# Validación — Verificación de paciente con seis caracteres

Fecha: 2026-11-05. Parche aprobado y aplicado por instrucción explícita del usuario.

## Resultado

PASS para el alcance implementado. No es aprobación de producción ni cierre de los asuntos aplazados.

- API: código criptográfico A–Z/0–9 de seis caracteres, asociado a cuenta y vencimiento mediante hash, validez de 15 minutos, un solo uso y cinco intentos fallidos persistidos.
- Flutter envía correo y código normalizado. El contrato /auth/verify requiere ambos campos.
- Respuestas comunes ApiResponse<T>/ErrorApiResponse adaptadas de api/api-response-models-template; conserva code/traceId.
- Migración 004 aplicada correctamente en la base configurada mediante run_local.py --configuration Release --no-build -- --migrate, salida 0. No se reinició la API habitual.
- Los tokens de verificación anteriores pendientes quedaron invalidados: se requiere reenvío.

## Pruebas ejecutadas

| Comprobación | Resultado |
| --- | --- |
| .NET 10: compilación Release y pruebas ejecutables | 12 aprobadas |
| Flutter analyze | Sin incidencias |
| Flutter test | 6 aprobadas |
| HTTP con PostgreSQL 17 temporal | 14 aprobadas |
| SMTP local: reintento, deduplicación y código de un uso | Aprobada |
| Playwright Chromium | 7 aprobadas |
| git diff --check sobre fuentes afectadas | Sin errores |

El coordinador termina con Synthetic milestone checks: PASS. El contenedor PostgreSQL temporal se elimina al finalizar y los servidores temporales se detienen. No se usó Gmail en estas pruebas ni se publicaron cambios.

## Incidencias corregidas durante la integración

- Sobrecarga de UseStatusCodePages/WriteAsJsonAsync: se especificaron StatusCodeContext y opciones JSON explícitas.
- La ampliación de pruebas agotaba la cuota real de autenticación: se reguló el ritmo de las solicitudes sintéticas y se reinició la API temporal entre suites HTTP y navegador, sin reducir la protección del producto. Las pruebas de intento/uso único siguieron persistiendo en PostgreSQL.

Los artefactos del navegador se encuentran en hitos/portalweb/primer-hito-portalweb/evidencias/playwright/.
