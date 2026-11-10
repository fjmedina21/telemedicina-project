# Implementación propuesta M01
Ejecución STAFF-EMAIL-2026-11-09-v1. Spec STAFF-INVITATION-EMAIL-SPEC-v1 y aprobación humana registrada.
Entrega: proposal.patch. NO aplicado al código fuente.

## Archivos afectados
- API: StaffInvitations, StaffController, AuthController, MailWorker.
- Portal: staff-panel.tsx y staff-activation-form.tsx.
- Pruebas: Program.cs, nuevo StaffInvitationUnitChecks.cs y smtp_fixture_test.py.

## Diseño
Emisión, revocación, auditoría y encolado comparten transacción. Reutiliza NotificationService y cifrado SMTP existentes.
El worker admite solo la invitación exacta vigente para personal pendiente, sin relajar reglas de correo ordinario.
Revoke comparte mailuser con el worker y anula mensajes Queued/Sending; limpia Unknown sin reintentarlo ni perder su estado.
Después de obtener bloqueo, el worker relee estado/lease y usa contador de intentos actualizado.
Caducidad de invitación o correo produce Expired; token inválido/no encontrado/consumido produce Superseded.
Token fuera de URLs, respuestas, logs y auditoría; dev-mailbox se cierra en SMTP.
Portal confirma registro, sin afirmar entrega.

## Criterios
Implementados todos los comportamientos de la spec. No se modifica producción, Flutter, permisos ni DTOs.
Incluye unitarios de origen portal y extensión opt-in de SMTP local para recepción de cuenta pendiente, activación de un uso, privacidad buzón, cooldown y denegación al personal.
No se agregan migraciones ni dependencias.

## Verificaciones
NO ejecutadas por Implementer: build API/portal, suite .NET, SMTP local y concurrencia en PostgreSQL.
El código fuente sigue intacto: no es posible atribuir pruebas de implementación a este checkout hasta aplicar la propuesta de forma autorizada.
El orquestador puede ejecutar git apply --check y validación de sintaxis del fixture sobre contenido de propuesta sin aplicar.

Pruebas propuestas tras revisión/aplicación:
- dotnet run --configuration Release --project api/src/Telemedicina.Tests/Telemedicina.Tests.csproj
- dotnet build --configuration Release api/src/Telemedicina.Api/Telemedicina.Api.csproj
- npm run build en portalweb/src
- MILESTONE_TEST_SYNTHETIC=1 MILESTONE_CONFIGURATION=Release python3 api/src/Telemedicina.Tests/Integration/smtp_fixture_test.py --staff-invitations

Para SMTP: Python compatible con smtpd (como fixture actual), base sintética separada con migraciones existentes y administrador sintético ya sembrado; entorno Development/Milestone, conexión, mailbox y PayloadKey privados. El fixture usa solo loopback, no imprime credenciales, no envía correo real y no migra/siembra/borrra datos.
Para regresión H02: modo development y fixture existente second_milestone_e2e.py.
Pendientes específicos: confirmar rollback si origen/cifrado/auditoría fallan; expiración; reenvío autorizado tras 60s; carreras deterministas worker con lease tomado y revocación/activación/cambio de rol; ordinary inactive sigue Superseded; retry/Unknown.

## Riesgos
SMTP ya aceptado no puede retirarse; su token sí queda invalidado.
La espera por mailuser puede retrasar operación administrativa mientras SMTP agota timeout existente.
La validación de origen rechaza subrutas y HTTP no loopback; usar origen HTTPS válido.
No garantiza recepción real SMTP ni funcionamiento en producción.

## Handoff Validator
Revisar diff frente spec, serialización mailuser/lease, privacidad y compatibilidad. Emitir recomendación distinguiendo inspección de pruebas ejecutadas.
