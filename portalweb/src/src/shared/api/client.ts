export type Item = Record<string, unknown>;
export class ApiError extends Error {
  constructor(public status: number, message: string, public code?: string) { super(message); }
}
let refreshing: Promise<unknown> | null = null;
export async function api<T>(path: string, method = "GET", body?: unknown, retry = true): Promise<T> {
  let response: Response;
  try {
    response = await fetch("/api/backend" + path, {
      method, credentials: "same-origin", cache: "no-store", signal: AbortSignal.timeout(15000),
      headers: {"Content-Type":"application/json","X-Telemedicina-Client":"milestone","X-Session-Mode":"cookie"},
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch { throw new ApiError(503,"No se pudo conectar con la API."); }
  if (response.status === 401 && retry && (path==="/auth/me" || !path.startsWith("/auth/"))) {
    refreshing ??= api("/auth/refresh","POST",{},false).finally(() => {refreshing=null;});
    await refreshing;
    return api<T>(path,method,body,false);
  }
  let parsed: {data?: T; title?: string; detail?: string; code?: string};
  try { parsed=await response.json(); }
  catch { throw new ApiError(response.status,"Respuesta del servidor no válida."); }
  if (!response.ok) {
    if (response.status===401 && typeof window!=="undefined")
      window.dispatchEvent(new Event("tm-session-invalidated"));
    const message=response.status===401 && path!=="/auth/login" ? "La sesión ya no está disponible." :
      response.status===403 ? "No tiene permiso para realizar esta acción." :
      response.status===409 ? "La cuenta cambió o la operación no está permitida. Recargue antes de continuar." :
      response.status===429 ? "Demasiados intentos. Espere un minuto antes de reintentar." :
      parsed.title ?? parsed.detail ?? "Solicitud rechazada.";
    throw new ApiError(response.status,message,parsed.code);
  }
  return parsed.data as T;
}
