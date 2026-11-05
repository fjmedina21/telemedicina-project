export type Item = Record<string, unknown>;
export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
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
  if (response.status === 401 && retry && !path.startsWith("/auth/")) {
    refreshing ??= api("/auth/refresh","POST",{},false).finally(() => {refreshing=null;});
    await refreshing;
    return api<T>(path,method,body,false);
  }
  let parsed: {data?: T; title?: string; detail?: string};
  try { parsed=await response.json(); }
  catch { throw new ApiError(response.status,"Respuesta del servidor no válida."); }
  if (!response.ok) throw new ApiError(response.status,parsed.title ?? parsed.detail ?? "Solicitud rechazada.");
  return parsed.data as T;
}
