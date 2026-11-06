# Guía de inicio local

El script `inicio_local.sh` coordina la API y el portal web. También puede lanzar la app Flutter en un simulador o dispositivo conectado.

## Requisitos

- .NET SDK 10
- Python 3
- Node.js/npm y dependencias instaladas en `portalweb/src/node_modules`
- PostgreSQL disponible y `api/src/.env` configurado de forma privada
- Flutter SDK y dispositivo/emulador para ejecutar la app

El script usa `Development`, activa las rutas locales del hito y usa `Mail__Mode=development` por defecto. No imprime ni necesita poner secretos en la línea de comandos.

## Iniciar API y portal

Desde la raíz del repositorio:

```bash
bash inicio_local.sh start
```

Al quedar listos:

- Portal: http://localhost:3000/login
- API: http://localhost:5000/api/v1/health
- Swagger: http://localhost:5000/swagger

La API puede tardar en compilar la primera vez. Los procesos quedan en segundo plano. El script no ejecuta migraciones ni carga datos seed. Si la base aún no tiene el esquema, aplica primero las migraciones mediante los pasos de [api/src/README.md](api/src/README.md).

## Ejecutar la app Flutter

Lista los dispositivos y simuladores disponibles:

```bash
cd app/src
flutter devices
cd ../..
```

Luego usa el ID del destino:

```bash
bash inicio_local.sh app <device-id>
```

La app corre en primer plano para mostrar los logs y permitir usar Flutter Hot Reload. Para detener la app, presiona `Ctrl+C` en esa terminal. API y portal permanecen activos.

Por defecto, la app usa `http://127.0.0.1:5000/api/v1`, adecuado para el simulador en el Mac. Para un iPhone conectado, inicia los servicios escuchando en la interfaz de red del Mac y pasa la URL LAN del Mac:

```bash
TELEMEDICINA_API_LISTEN_URL=http://0.0.0.0:5000 bash inicio_local.sh start
bash inicio_local.sh app <device-id> http://<ip-del-mac>:5000/api/v1
```

El iPhone y el Mac deben tener conectividad entre sí. No uses `127.0.0.1` como dirección de la API en un iPhone físico: ese valor apuntaría al propio teléfono.

## Estado y detención

```bash
bash inicio_local.sh status
bash inicio_local.sh stop
```

`stop` detiene los procesos de API y portal que inició este script. Cierra Flutter con `Ctrl+C` en su propia terminal.

Los logs y PID temporales se guardan en:

```bash
${TMPDIR:-/tmp}/telemedicina-local
```

## Cambiar el puerto de la API

Si el puerto 5000 está ocupado, configura otro puerto para API y proxy:

```bash
TELEMEDICINA_API_PORT=5081 bash inicio_local.sh start
```

Para que un iPhone físico se conecte por ese puerto:

```bash
TELEMEDICINA_API_PORT=5081 TELEMEDICINA_API_LISTEN_URL=http://0.0.0.0:5081 bash inicio_local.sh start
bash inicio_local.sh app <device-id> http://<ip-del-mac>:5081/api/v1
```

Usa `http://localhost:3000` para entrar al portal; su origen coincide con el permitido por la configuración local de la API.
