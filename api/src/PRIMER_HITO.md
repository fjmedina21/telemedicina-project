# Primer hito — contratos y operación de desarrollo

Implementación para datos sintéticos sobre .NET 10. La reorganización conserva las rutas HTTP,
las formas de respuesta, los controles de acceso y los datos PostgreSQL existentes.

## Configuración privada

Tools/run_local.py lee api/src/.env sin expansión de shell y respeta variables ya definidas.
El archivo real está ignorado por Git y tiene permisos 0600. .env.example contiene solo plantillas.

| Variable | Uso |
|---|---|
| ConnectionStrings__Telemedicina | URI PostgreSQL privada o cadena Npgsql |
| ASPNETCORE_ENVIRONMENT | Development |
| Milestone__Enabled | true para habilitar este incremento |
| Milestone__PortalOrigin | Origen exacto del portal; por defecto http://localhost:3000 |
| Milestone__MailboxKey | Acceso al buzón de desarrollo |
| Milestone__AdmissionEmail / AdmissionPassword | Seed sintético de admisión |
| Mail__Mode | smtp para correo real; development para captura sintética |
| Mail__Host / Port / Username / Password / Sender | Configuración SMTP privada; Gmail local usa smtp.gmail.com:587 con STARTTLS |
| Mail__PayloadKey | 32 bytes aleatorios en Base64 para cifrar cargas SMTP |

No se requieren DOTNET_ROLL_FORWARD ni un runtime 9 para ejecutar esta versión.
Las migraciones 001–004 están en Infrastructure/Persistence/Migrations y se aplican en orden
únicamente con --migrate. El esquema es telemedicina_m1; no se reinicia ni elimina la base.

## Contratos HTTP

Todas las rutas siguientes parten de /api/v1. UUID públicos; fechas UTC y visualización
America/Santo_Domingo. Éxito: {success,status,detail,data}. Errores conservan sus estados HTTP
y usan ErrorApiResponse con code/traceId, incluyendo la validación de DTO.

| Rutas | Acceso y propósito |
|---|---|
| POST /auth/register, /verification/resend, /verify | Registro/verificación y reenvío limitado |
| POST /auth/login | Correo y contraseña |
| POST /auth/refresh, /auth/logout | Rotación y revocación por familia de sesión |
| POST /auth/password/forgot, /auth/password/reset | Recuperación genérica y token de un uso |
| GET /auth/me | Identidad y rol del servidor |
| GET /auth/dev-mailbox?email=...&purpose=verify/reset | Solo development y clave de desarrollador |
| GET/PATCH /patients/me | Perfil propio, versión y fuente autodeclarada |
| GET /specialties, /services, /practitioners, /availability | Catálogo activo y disponibilidad libre |
| POST /appointments; GET /appointments/me | Solicitud con Idempotency-Key y citas propias |
| GET /appointments/{id}, /appointments/{id}/history | Dueño o admisión |
| POST /appointments/{id}/cancel, /reschedule | Dueño y versión vigente |
| GET /admin/work-queue | Cola de admisión |
| PATCH /admin/appointments/{id}/validation | Revisión manual con evidencia y vigencia |
| GET/POST /admin/catalog/services, /practitioners | Gestión del catálogo |
| PATCH /admin/catalog/services/{id}, /practitioners/{id} | Edición con control de versiones |
| POST /admin/slots; PATCH /admin/slots/{id}/block | Horarios sin solapamientos y bloqueos |
| GET /notifications/me; POST /notifications/{id}/read | Avisos privados y lectura |
| GET/PATCH /notifications/preferences | Opt-in/opt-out de avisos por correo |
| GET /admin/email-delivery | Estado de entrega sin destinatarios, cuerpos ni credenciales |
| GET /admin/audit-events; PATCH /admin/users/{id}/active | Auditoría y suspensión/reactivación de pacientes |

Las rutas abreviadas de autenticación mantienen el prefijo /auth para cada operación.
Admisión se autoriza en servidor; el cliente no concede roles.

## Invariantes conservadas

- Restricción PostgreSQL contra solapamientos, transacciones e idempotencia por usuario/solicitud.
- Servicios/profesionales activos y asignación profesional-servicio para publicar disponibilidad.
- La referencia de habilitación del catálogo es registrada por el operador; no valida credenciales clínicas automáticamente.
- Cita y cobertura son estados independientes. Reprogramar devuelve la cita a revisión.
- Cancelar/reprogramar conserva historial. Las versiones impiden sobrescrituras silenciosas.
- Sesiones revocables, refresh de un uso y tokens fuera de logs y almacenamiento web.
- Cookies del portal HttpOnly/SameSite=Strict y cabecera anti-CSRF; Secure cuando la API usa HTTPS.
- Auditoría e historial protegidos contra UPDATE/DELETE/TRUNCATE; antes de datos reales faltan controles de privilegios/retención.

## Notificaciones y correo aplazado

Los avisos de solicitud, revisión, cancelación y reprogramación se guardan en la transacción.
El paciente ve el historial dentro de la app. Los avisos por correo requieren opt-in; la recuperación
y verificación de cuenta se solicitan explícitamente. Los mensajes no incorporan contenido clínico.

La outbox tiene una clave única por evento, leases y reintentos acotados. Captured identifica el modo
de desarrollo. Una aceptación SMTP incierta se marca Unknown y no se reenvía automáticamente:
SMTP no permite prometer entrega exactamente una vez ante una confirmación perdida.
Cargas sensibles se cifran en modo SMTP y se eliminan al finalizar su estado terminal.

Se validó previamente la autenticación Gmail y la aceptación SMTP de un correo de verificación;
no se inspeccionó la bandeja de entrada. En Development se permite desactivar únicamente la
comprobación de revocación mediante Mail__CheckCertificateRevocation=false para el fallo local
RevocationStatusUnknown; confianza, vigencia y nombre del certificado siguen validados.
Fuera de Development, la comprobación de revocación es obligatoria.
Las pruebas automatizadas usan SMTP local y no envían correo real.

## Validación y tareas diferidas

La batería de contratos, HTTP, SMTP simulado, widgets y Playwright valida el incremento.
Las pruebas de navegador usan Chromium real; no almacenan credenciales ni storageState.
Resultados y artefactos por componente: ../../hitos/.

Aplazados por el momento: políticas operativas, revisión PR/UAT/pipeline remoto y validación del
correo en producción. Android/iOS fue retirado de los pendientes por el usuario.
La CI está preparada y se valida localmente mediante el coordinador;
su ejecución remota necesita publicar los cambios en una rama autorizada del repositorio público.

## Verificación de registro del paciente

`POST /api/v1/auth/verify` recibe `{ "email": "synthetic@example.invalid", "token": "AB12CD" }`.
El código tiene seis caracteres A–Z/0–9, acepta minúsculas y caduca en 15 minutos.
Está asociado al correo, permite un solo uso y queda invalidado tras cinco intentos fallidos.
El reenvío conserva el intervalo mínimo de un minuto y sustituye el código anterior.
La migración 004 invalida los tokens de verificación anteriores: las cuentas pendientes deben solicitar reenvío.
La recuperación de contraseña mantiene su token largo y el portal usa correo y contraseña.

Las respuestas usan los modelos de `Telemedicina.Common.Models.ApiResponses`, adaptados de
`api/api-response-models-template`: `ApiResponse<T>` y `ErrorApiResponse`; los listados
existentes conservan su contrato, sin agregar paginación implícita.
