# Validación ejecutada H02 — 2026-11-05

La corrección de compilación y la del listado de personal fueron aprobadas por el usuario y aplicadas.
La revalidación confirma el listado, sus filtros y los escenarios funcionales previstos.
Cierre funcional local aprobado por el usuario el 2026-11-05. PR, UAT y go/no-go de producción siguen pendientes.

| Comprobación | Resultado verificado |
| --- | --- |
| API .NET 10 Release | PASS, cero errores y advertencias |
| Portal Next.js | PASS |
| Flutter macOS Release | PASS |
| Análisis Flutter | PASS, sin incidencias |
| Pruebas .NET | 15 aprobadas; repetidas tras corregir API |
| Pruebas Flutter | 15 aprobadas |
| Migración 005 en base configurada | PASS |
| Reejecución de migraciones | PASS |
| OpenAPI generado | 44 rutas; contratos H02 presentes |
| HTTP primer hito | 14 aprobadas |
| HTTP segundo hito | 8 aprobadas tras la corrección, incluidos listado y filtros |
| SMTP local loopback | PASS |
| Chromium primer hito | 7 aprobadas |
| Chromium segundo hito | PASS, 3/3 escenarios en una corrida completa con token bucket |

## Revalidación Playwright y limitador

La corrida completa del 2026-11-05 ejecutó los tres escenarios Chromium en serie y pasó 3/3
en38.3s. Antes, la ventana fija compartida producía429 en el caso de replay de cookies.
Se sustituyó por token bucket con capacidad30, reposición de1 token cada2s y cola0. La tasa
sostenida queda en30 solicitudes/minuto; el burst está acotado a30 y no se amplió la capacidad.
No se deshabilitó la protección.

La evidencia Playwright actual está en `hitos/portalweb/segundo-hito-portalweb/evidencias/playwright/playwright-results.json`.

## Evidencia

- resultados.json y logs iniciales: primera ejecución, incluidos los fallos detectados.
- revalidacion.json y logs revalidacion-*: repetición tras aplicar el parche aprobado.
- revalidacion-browser-aislado.json y .log: caso de cookies aprobado en aislamiento.
- resumen-final.json: resumen consolidado; suite Chromium 3/3 y cierre funcional local.
- Informes Playwright bajo hitos/portalweb/*/evidencias/playwright; el último informe H02 es el caso aislado.

Se utilizaron cuentas y datos sintéticos nuevos. No se enviaron correos externos ni se modificó
la configuración privada. Los datos sintéticos creados permanecen en la base; las credenciales
no se guardaron en informes. Los servicios temporales se detuvieron al terminar.

El hito queda cerrado en su alcance funcional local por instrucción explícita del usuario.
Siguen pendientes PR, UAT, go/no-go de piloto/producción, validación dirigida de expiración y
reenvío de invitaciones, fallo inyectado de auditoría/rollback, segundo administrador como
destinatario, carga prolongada y accesibilidad completa.
