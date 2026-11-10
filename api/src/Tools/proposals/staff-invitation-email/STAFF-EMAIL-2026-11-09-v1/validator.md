# Reporte de validación M01
Ejecución: STAFF-EMAIL-2026-11-09-v1.
Spec: STAFF-INVITATION-EMAIL-SPEC-v1.
Parche: proposal.patch, 9 archivos.
SHA256: bd0500ea661e2a21da93d539cbc20df19fed72d0b4d28cfdedefffad0057bada.

## Veredicto provisional: PASS de revisión estática
Recomendación del agente Validator para revisión humana. No certifica compilación, entrega SMTP ni aceptación funcional.
Constancia de aprobación corresponde a la spec revisada; diff limitado a api/src/ y portalweb/src/.
Sin hallazgos bloqueantes en la propuesta.

## Matriz de criterios
| Criterio | Evidencia estática | Verificación pendiente |
|---|---|---|
| Atomicidad cuenta/invitación/correo/auditoría | Issue comparte transacción del controlador y NotificationService.Queue | Rollback por fallo de origen, cifrado o auditoría |
| Personal pendiente | Worker exige invitación exacta vigente/no usada, rol gestionable y cuenta pendiente | Recepción SMTP local |
| Invalidación/concurrencia | Revoke comparte mailuser, invalida Queued/Sending y limpia Unknown; worker relee estado/lease | Carreras deterministas con lease tomado |
| Vencimiento/reenvío | Expiración, versión y cooldown conservados | Expiración y reenvío autorizado tras 60s |
| Privacidad | Sin tokens en URLs/DTOs; cuerpo SMTP cifrado; buzón rechazado en SMTP | HTTP y persistencia |
| Autorización/compatibilidad | Servicios del constructor registrados en DI; mismos contratos HTTP | Regresión identidad/H02 y builds |
| Portal | Registro pendiente de entrega y activación por token recibido | Build y recorrido navegador |

Fixture SMTP opt-in propone PendingActivation, campos públicos exactos, mailbox404, cooldown429, recepción/token, enlace sin secreto, activación única y Support403.
Unitarios positivos/negativos de origen del portal incluidos.

## Evidencia realmente ejecutada por orquestador
- git apply --check --whitespace=error-all sobre hash final: salida 0.
- Reconstrucción en memoria de fixture Python propuesto y ast.parse: PASS.
- Comprobación áreas de los 9 archivos: PASS.
- Hash final coincide con versión revisada por Validator.
No se aplicó el parche. git diff de fuente vacío al revisar; solo artefactos nuevos en api/src/Tools/proposals/staff-invitation-email/.
El sistema emitió diagnósticos auxiliares de caché xcrun por sandbox; las comprobaciones anteriores finalizaron con salida 0.

## Pendientes antes de integración por PR
Build API/portal, unitarios, SMTP local sintético, regresión H02; carreras, vencimiento, rollback, reenvío tras cooldown y correo ordinario de inactivos.
Validator no ejecutó shell, pruebas, aplicación del parche ni despliegue. Revisión por lectura de archivos.
SMTP ya aceptado puede llegar tras revocación; token inválido impide activación. Bloqueo puede demorar administración durante envío.
Alcance Development, sin producción ni decisiones clínicas.

## Próximo paso
Revisión humana del parche concreto antes de aplicarlo y ejecutar las verificaciones funcionales con datos sintéticos.
Regla: .ai/agents/implementer.md permite proponer un único parche; no aplicarlo.
