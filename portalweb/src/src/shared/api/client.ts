export type Item = Record<string, unknown>;
export class ApiError extends Error {
  constructor(public status: number, message: string, public code?: string) { super(message); }
}
export class StaleResponse extends Error {
  constructor(){super("La respuesta pertenece a una consulta de sesión anterior.");}
}
let sessionGeneration=0;
let refreshing: Promise<unknown> | null = null;
export async function api<T>(path: string, method = "GET", body?: unknown, retry = true,
  extraHeaders: Record<string,string> = {}): Promise<T> {
  // This only guards UI results; browser cookie application remains server-controlled.
  if(path==="/auth/login"||path==="/auth/logout")sessionGeneration++;
  const generation=sessionGeneration;
  let response: Response;
  try {
    response = await fetch("/api/backend" + path, {
      method, credentials: "same-origin", cache: "no-store", signal: AbortSignal.timeout(15000),
      headers: {"Content-Type":"application/json","X-Telemedicina-Client":"milestone","X-Session-Mode":"cookie",...extraHeaders},
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    if(generation!==sessionGeneration)throw new StaleResponse();
    throw new ApiError(503,"No se pudo conectar con la API.");
  }
  if(generation!==sessionGeneration)throw new StaleResponse();
  if (response.status === 401 && retry && (path==="/auth/me" || !path.startsWith("/auth/"))) {
    refreshing ??= api("/auth/refresh","POST",{},false).finally(() => {refreshing=null;});
    await refreshing;
    if(generation!==sessionGeneration)throw new StaleResponse();
    return api<T>(path,method,body,false,extraHeaders);
  }
  let parsed: {data?: T; title?: string; detail?: string; code?: string};
  try { parsed=await response.json(); }
  catch {
    if(generation!==sessionGeneration)throw new StaleResponse();
    throw new ApiError(response.status,"Respuesta del servidor no válida.");
  }
  if(generation!==sessionGeneration)throw new StaleResponse();
  if (!response.ok) {
    if(response.status===401){
      sessionGeneration++;
      if(typeof window!=="undefined")window.dispatchEvent(new Event("tm-session-invalidated"));
    }
    const message=response.status===401 && path!=="/auth/login" ? "La sesión ya no está disponible." :
      response.status===403 ? "No tiene permiso para realizar esta acción." :
      response.status===409 ? "La cuenta cambió o la operación no está permitida. Recargue antes de continuar." :
      response.status===429 ? "Demasiados intentos. Espere un minuto antes de reintentar." :
      parsed.title ?? parsed.detail ?? "Solicitud rechazada.";
    throw new ApiError(response.status,message,parsed.code);
  }
  return parsed.data as T;
}
