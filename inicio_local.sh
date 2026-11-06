#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$ROOT/api/src"
PORTAL_DIR="$ROOT/portalweb/src"
APP_DIR="$ROOT/app/src"
RUN_DIR="${TMPDIR:-/tmp}/telemedicina-local"
API_PID_FILE="$RUN_DIR/api.pid"
PORTAL_PID_FILE="$RUN_DIR/portalweb.pid"
API_LOG="$RUN_DIR/api.log"
PORTAL_LOG="$RUN_DIR/portalweb.log"
API_PORT="${TELEMEDICINA_API_PORT:-5000}"
API_URL="${TELEMEDICINA_API_BASE_URL:-http://127.0.0.1:$API_PORT/api/v1}"
API_HEALTH="http://127.0.0.1:$API_PORT/api/v1/health"
API_LISTEN_URL="${TELEMEDICINA_API_LISTEN_URL:-http://127.0.0.1:$API_PORT}"
PORTAL_URL="http://localhost:3000/login"

usage() {
  cat <<'USAGE'
Uso:
  bash inicio_local.sh start
  bash inicio_local.sh stop
  bash inicio_local.sh status
  bash inicio_local.sh app <device-id> [api-base-url]

start inicia la API y el portal web. No ejecuta migraciones ni seeds.
app inicia ambos servicios si hace falta y luego ejecuta Flutter en primer plano.
Para simulador, usa http://127.0.0.1:5000/api/v1 como API.
Para iPhone físico, pasa la URL LAN del Mac, por ejemplo http://<ip-del-mac>:5000/api/v1.
En ese caso, inicia con TELEMEDICINA_API_LISTEN_URL=http://0.0.0.0:5000 para aceptar conexiones LAN.
Si el puerto 5000 está ocupado, usa TELEMEDICINA_API_PORT=<puerto> al iniciar.
Ctrl+C en la terminal de Flutter cierra la app; `stop` detiene API y portal.
USAGE
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || { printf 'Falta el comando requerido: %s\n' "$1" >&2; exit 1; }
}

wait_for_url() {
  local url="$1" label="$2" attempt=0
  while [ "$attempt" -lt 45 ]; do
    if curl -fsS "$url" >/dev/null 2>&1; then return 0; fi
    sleep 1
    attempt=$((attempt + 1))
  done
  printf '%s no respondió a tiempo. Revisa el log: %s\n' "$label" "$3" >&2
  return 1
}

start_api() {
  if curl -fsS "$API_HEALTH" >/dev/null 2>&1; then
    printf 'API ya responde en http://localhost:5000\n'
    return
  fi
  [ -f "$API_DIR/.env" ] || { printf 'Falta api/src/.env privado; revisa api/src/README.md.\n' >&2; return 1; }
  mkdir -p "$RUN_DIR"
  (
    cd "$ROOT"
    export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
    export ASPNETCORE_URLS="${ASPNETCORE_URLS:-$API_LISTEN_URL}"
    export Milestone__Enabled="${Milestone__Enabled:-true}"
    export Milestone__PortalOrigin="${Milestone__PortalOrigin:-http://localhost:3000}"
    export Mail__Mode="${Mail__Mode:-development}"
    nohup python3 api/src/Tools/run_local.py --configuration Release >"$API_LOG" 2>&1 </dev/null &
    echo "$!" >"$API_PID_FILE"
  )
  wait_for_url "$API_HEALTH" "La API" "$API_LOG"
  printf 'API lista: http://localhost:%s/swagger\n' "$API_PORT"
}

start_portal() {
  if curl -fsS "$PORTAL_URL" >/dev/null 2>&1; then
    printf 'Portal ya responde en http://localhost:3000\n'
    return
  fi
  [ -x "$PORTAL_DIR/node_modules/.bin/next" ] || {
    printf 'Faltan dependencias del portal. Ejecuta: cd portalweb/src && npm ci --ignore-scripts\n' >&2
    return 1
  }
  mkdir -p "$RUN_DIR"
  (
    cd "$PORTAL_DIR"
    export API_INTERNAL_URL="${API_INTERNAL_URL:-$API_URL}"
    nohup npm run dev >"$PORTAL_LOG" 2>&1 </dev/null &
    echo "$!" >"$PORTAL_PID_FILE"
  )
  wait_for_url "$PORTAL_URL" "El portal" "$PORTAL_LOG"
  printf 'Portal listo: %s\n' "$PORTAL_URL"
}

start_services() {
  require_command python3
  require_command dotnet
  require_command npm
  require_command curl
  start_api
  start_portal
}

managed_command_matches() {
  local pid="$1" pattern="$2"
  ps -p "$pid" -o command= 2>/dev/null | grep -E "$pattern" >/dev/null 2>&1
}

stop_tree() {
  local pid="$1" children child
  kill -0 "$pid" 2>/dev/null || return 0
  children="$(pgrep -P "$pid" 2>/dev/null || true)"
  for child in $children; do stop_tree "$child"; done
  kill -TERM "$pid" 2>/dev/null || true
}

stop_managed() {
  local name="$1" file="$2" pattern="$3" pid
  if [ ! -f "$file" ]; then printf '%s: no hay proceso gestionado por este script.\n' "$name"; return; fi
  pid="$(cat "$file")"
  if kill -0 "$pid" 2>/dev/null && managed_command_matches "$pid" "$pattern"; then
    stop_tree "$pid"
    printf '%s detenido.\n' "$name"
  else
    printf 'PID antiguo de %s; no se terminó ningún proceso ajeno.\n' "$name"
  fi
  rm -f "$file"
}

stop_services() {
  stop_managed "Portal" "$PORTAL_PID_FILE" 'npm run dev|next dev -p 3000'
  stop_managed "API" "$API_PID_FILE" 'run_local.py|Telemedicina.Api'
}

show_status() {
  require_command curl
  if curl -fsS "$API_HEALTH" >/dev/null 2>&1; then printf 'API: activa (http://localhost:5000)\n'; else printf 'API: detenida\n'; fi
  if curl -fsS "$PORTAL_URL" >/dev/null 2>&1; then printf 'Portal: activo (http://localhost:3000)\n'; else printf 'Portal: detenido\n'; fi
  printf 'Logs: %s\n' "$RUN_DIR"
}

command_name="${1:-help}"
case "$command_name" in
  start)
    start_services
    ;;
  stop)
    stop_services
    ;;
  status)
    show_status
    ;;
  app)
    [ "$#" -ge 2 ] || { usage >&2; exit 2; }
    require_command flutter
    start_services
    device_id="$2"
    api_url="${3:-$API_URL}"
    cd "$APP_DIR"
    exec flutter run -d "$device_id" --dart-define="API_BASE_URL=$api_url"
    ;;
  help|-h|--help)
    usage
    ;;
  *)
    usage >&2
    exit 2
    ;;
esac
