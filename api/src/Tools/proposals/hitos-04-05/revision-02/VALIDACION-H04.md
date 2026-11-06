# Revisión H04 — continuación del 2026-11-05

Estado: CHANGES_REQUIRED; correcciones propuestas para nueva revisión. H04 no validado funcionalmente. H05 pendiente de H04.

## Autorización y alcance

Constancia existente: aprobacion-H04-H05-v1.md; las huellas actuales de H04-SPEC-v1 y H05-SPEC-v1 coinciden con la aprobación registrada. Se mantiene la secuencia H04 → H05 y el alcance P0. Las instrucciones actuales del usuario exigen proponer parches sin aplicarlos automáticamente. Esta continuación solo prepara artefactos en las áreas Tools/proposals autorizadas.

## Revisión de Validator y correcciones de propuesta

- Backend: el helper de comprobaciones PostgreSQL intentaba leer data también en errores 400/409; separar comprobación del envelope de error y verificar códigos.
- Backend: la disponibilidad final consultaba now() de PostgreSQL, fijado al comienzo de transacción; comprobar un instante UTC actual después de esperar los locks. Añadir regresión que haga cruzar el inicio del slot durante la espera.
- Flutter: faltaba toda prueba del catálogo H04; añadir pruebas propuestas de fechas, serialización/filtros, respuestas antiguas, cierre de sesión y conservación de borradores.
- Portal: una respuesta antigua podía invalidar una nueva sesión durante el parseo JSON; revalidar la generación después del parseo y en su fallo.
- Portal: tras creación con resultado incierto se podía repetir POST sin consultar; exigir una consulta satisfactoria antes de volver a crear.
- Portal: la configuración Playwright compartía destino de evidencias con H01; proponer una configuración H04 separada dentro del área de portal.

No se cambian las specs aprobadas, los contratos de H05 ni código activo de la aplicación. Los documentos antiguos que dicen "pendiente aprobación" son históricos; el estado de autorización vigente está en aprobacion-H04-H05-v1.md y estado-ejecucion.json.

## Evidencia ejecutada

Las tres propuestas originales pasan git apply --check (código 0). El sistema emitió avisos de caché xcrun por el sandbox; no hubo rechazos de hunks. Las huellas SHA-256 de ambas specs coinciden con la aprobación. Validator contrastó las propuestas con el código y las specs en solo lectura.

Portal: reproducción aislada en memoria del fallo de JSON tardío: oldResult=ApiError y newSessionInvalidations=1. Diagnóstico de sintaxis TypeScript en memoria de los seis archivos originales: cero diagnósticos. Estas comprobaciones no equivalen a build, pruebas de navegador ni integración HTTP.

## Propuestas corregidas de esta continuación

Los parches originales permanecen conservados. La revisión 02 está en:
- API: revision-02/h04-backend.patch
- Flutter: app/src/Tools/proposals/hitos-04-05/revision-02/h04-flutter.patch
- Portal: portalweb/src/Tools/proposals/hitos-04-05/revision-02/h04-portal.patch

Las tres propuestas corregidas pasan juntas git apply --check por entrada estándar, sin aplicarse. Se ejecutó en memoria la nueva suite Node contra el cliente propuesto: 3/3 PASS (JSON tardío 200, 401 e inválido; cero invalidaciones de sesión nueva). Transpilación TypeScript en memoria: siete archivos sin diagnósticos de sintaxis; no equivale a comprobación completa de tipos ni build.

La propuesta Flutter añade nueve tests (cinco de repositorio/fechas y cuatro de controlador); no se ejecutaron. La propuesta backend corrige el helper de errores y añade un caso PostgreSQL que espera el lock hasta cruzar el inicio del slot; no se ejecutó. La propuesta Playwright añade reconciliación tras POST confirmado cuya respuesta se pierde; no se ejecutó.

Comandos tras integración revisada del portal, desde portalweb/src:
- node --test tests/unit/session-generation.test.cjs
- npx playwright test --config playwright.h04.config.ts

## Verificación aún requerida

Revisar las propuestas corregidas; compilar .NET y ejecutar su suite de consola y PostgreSQL; flutter analyze y flutter test; build del portal y Playwright con configuración H04. Ejecutar además HTTP/OpenAPI y matriz de roles, cruces de reservas/bloqueos/desactivaciones, intercambio de destinos ocupados, rollback al fallar auditoría/outbox y regresiones H03/citas.

Las nuevas pruebas son propuestas hasta ejecutarlas sobre una integración revisada. No declarar PASS, aplicar migraciones ni marcar completado H04/H05 con esta revisión.

## Segunda revisión de Validator

Los hallazgos originales de backend y portal quedan resueltos por revisión estática. Validator confirma coherencia de los nueve tests Flutter propuestos. Se ajustó el caso PostgreSQL nuevo para observar pg_locks y evitar un snapshot de estadísticas obsoleto durante la espera. El parche backend corregido vuelve a pasar git apply --check. .NET, PostgreSQL, Flutter, build y Playwright siguen sin ejecutar; el estado funcional continúa CHANGES_REQUIRED hasta completar las verificaciones pendientes.
