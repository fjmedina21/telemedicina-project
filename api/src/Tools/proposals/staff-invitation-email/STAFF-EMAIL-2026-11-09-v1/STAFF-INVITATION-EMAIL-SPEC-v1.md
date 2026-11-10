# STAFF-INVITATION-EMAIL-SPEC-v1

Estado: aprobada por el usuario el 2026-11-09. Ejecución: STAFF-EMAIL-2026-11-09-v1.
Módulo M01; amplía H02, que excluía SMTP. Basada en el diseño Architect y la entrega Spec Creator de esta conversación.

## Alcance y contratos
Backend único responsable backend_mobile: api/src/. Portal: portalweb/src/. Sin cambios Flutter ni habilitación de producción.
Conservar Development + Milestone:Enabled y la autorización Administrator para Admission, Practitioner y Support.
Mantener POST /api/v1/admin/staff/invitations: 201 StaffAccount; POST /api/v1/admin/staff/{id}/invitation: 200 StaffAccount con expectedVersion; POST /api/v1/auth/staff/activate: resultado actual sin login automático.
Conservar validaciones y errores 400/401/403/404/409/429. Reenvío solo PendingActivation, intervalo mínimo 60 segundos y control de versión. Éxito confirma persistencia y encolado, no recepción.

## Datos y entrega
Reutilizar NotificationService.Queue, email_outbox, MailSettings, MailWorker y MailTransport.
Cuenta, invitación, correo y auditoría en una misma transacción.
Token aleatorio existente, hash persistido, un uso y 15 minutos. Cuerpo SMTP cifrado; token nunca en respuestas administrativas, logs, auditoría ni URLs.
Correo plano con token, vencimiento e instrucciones para definir contraseña en Milestone:PortalOrigin + /activar-personal.
Validar origen absoluto HTTP(S), sin credenciales, query ni fragmento; HTTP solo loopback local. Configuración inválida impide confirmar sin cambios parciales.
kind=staff-activation, event_key=staff-activation:<hash>; notification_id, resource_id y resource_version nulos. Clave única deduplica evento.
Worker permite correo a active=false solo para invitación exacta vigente/no usada, verified=false y rol gestionable. Mantener elegibilidad ordinaria.
development conserva captura protegida; SMTP no crea ni expone token mediante dev-mailbox, aun con clave válida.

## Concurrencia e invalidación
Reenvío, activación, cambio de rol y revocación invalidan invitaciones anteriores y correos pendientes, limpiando carga sensible.
Compartir bloqueo asesor mailuser:<id> entre emisión/revocación y worker; identidad mantiene primero bloqueos de usuarios.
Worker relee estado y lease tras bloqueo; si se invalidó mientras esperaba, no envía.
Mantener reintentos y Unknown existente; no reintentar Unknown automáticamente.
Correo ya aceptado por SMTP puede llegar tras revocación; su token será inutilizable.

## UX y fallos
Crear o reenviar: "Invitación registrada. La entrega del correo está pendiente."
Activación indica usar token recibido, elimina referencia exclusiva a desarrollo.
Mantener mensajes existentes de sesión, permisos, conflictos y límites. Fallo SMTP conserva pendiente y estados/reintentos existentes.

## Aceptación y verificación
1. Administrador invita rol permitido: cuenta pendiente, invitación, auditoría y un correo sin token en respuesta.
2. SMTP local sintético recibe correo para cuenta pendiente inactiva; token activa una sola vez.
3. Reenvío invalida token anterior y correo pendiente; antes de 60 segundos devuelve 429.
4. Activación, revocación o cambio de rol concurrente con lease impiden envío obsoleto.
5. Invitación vencida termina Expired y limpia carga.
6. SMTP rechaza recuperar token en dev-mailbox; desarrollo conserva captura protegida.
7. Otros roles y correo ordinario de inactivos mantienen restricciones.
Validar SMTP local con datos sintéticos, concurrencia, regresión identidad/H02 y compilación API/portal.
Implementer propone único patch; Validator revisa; integración posterior mediante revisión y pruebas.
