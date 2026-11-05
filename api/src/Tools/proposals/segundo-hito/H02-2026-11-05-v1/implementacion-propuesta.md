# Implementación propuesta H02
Estado: parche no aplicado. Aprobación humana registrada en este paquete.
Alcance: H02-SPEC-v1; único diff transversal dentro de áreas autorizadas.

Incluye migración 005, permisos cerrados, gestión/activación de personal, revocación y
control de versión, guardia transaccional compartida, auditoría saneada, seed sintético,
OpenAPI mediante DTOs/anotaciones, portal por capacidades y protección Flutter.
Conserva funciones Patient/Admission y limita los nuevos roles sin accesos clínicos.
Adaptación de prueba heredada contradictoria con política actual de contraseña,
conservando aserciones positivas y rechazo de contraseña incorrecta.

Pruebas propuestas: permisos y guardas seed en ejecutable .NET; ocho escenarios HTTP H02;
siete casos Flutter; tres Playwright H02. También regresión primer hito.
No se compiló ni ejecutó ninguna prueba funcional, servicio, migración, seed o navegador.
Únicamente verify_patch (validación de rutas y git apply --check --whitespace=error)
se ejecutó sobre el diff propuesto. No se aplicó.

Pendientes explícitos: validación funcional/concurrencia contra PostgreSQL, rollback por
fallo de auditoría, vencimiento/reenvío, segundo administrador como destinatario,
compilación .NET/TypeScript/Flutter, OpenAPI generado, accesibilidad, PR y UAT.
Los límites auth existentes se conservan:30/min/IP, cinco fallos de contraseña/bloqueo5min;
reenvío de invitación mínimo60s. Invitación15min/256bits.
No hay afirmación de seguridad o disponibilidad en producción.

Handoff Validator: revisar diff completo contra H02-SPEC-v1, con especial atención a
orden de bloqueos usuario/sesión/invitación, revalidación de las escrituras existentes,
compatibilidad de respuestas y ausencia de secretos o permisos heredados.

## Ajustes tras primera revisión de Validator
El primer pase fue CHANGES_REQUIRED (ver validator-primera-revision.md).
Se ajustó la propuesta para conservar refresh ante challenges de recursos protegidos;
solo el fracaso real de /auth/refresh y logout eliminan el par de cookies.
Se añadió escenario Playwright con expiración real de cookie access, renovación y replay
de familia revocada. Se adaptó la identidad del fixture Flutter de avisos.
StaffActiveRequest exige presencia JSON de active; el caso HTTP negativo comprueba400,
sesión aún válida y accountVersion sin cambios.
Estos ajustes están pendientes de re-revisión. No se declaran resueltos ni probados.

## Solicitud posterior del usuario
Retirada propuesta de escenarios/helpers obsoletos de segundo factor; ver limpieza-pruebas-autenticacion.md. Se conserva cobertura de contraseña, permisos y sesiones. Pendiente de re-revisión del hash actual.
