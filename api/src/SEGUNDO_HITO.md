# H02 — Roles y operación de cuentas

Spec aprobada: H02-SPEC-v1; ejecución H02-2026-11-05-v1.
Alcance local sintético. Este documento acompaña un parche propuesto y no afirma integración ni producción.

## Autoridad y límites

Una organización implícita y un rol por usuario. El registro público siempre crea Patient.
Patient y Admission conservan sus funciones. Administrator no hereda Admission.
Practitioner y Support solo disponen de identidad, refresh y logout.
Tutor, auditor, coordinador, expedientes, documentos y funciones clínicas permanecen fuera de alcance.

Solo Administrator gestiona Admission, Practitioner y Support. Ningún endpoint administra
Administrator, el propio actor ni pacientes por las nuevas rutas de personal.
Admission conserva su ruta existente para suspender/reactivar pacientes.

Permisos efectivos cerrados en AccessCatalog; no se admiten permisos del cliente.
El portal representa capacidades y la API comprueba cada operación.
Las lecturas de catálogo/disponibilidad/citas ahora limitan explícitamente Patient/Admission.

## Contrato

Prefijo /api/v1. Respuestas ApiResponse<T>; errores ErrorApiResponse con code/traceId.
Las anotaciones de controllers y DTOs alimentan el OpenAPI existente /openapi/v1.json.

| Ruta | Entrada | Salida data |
|---|---|---|
| GET /auth/me | sesión | userId,email,role,permissions[],accountVersion |
| GET /admin/staff | after UUID,limit 1–100 (20),role,status | items[{id,email,role,status,version}],next UUID/null |
| POST /admin/staff/invitations | email,role | cuenta pendiente; 201; sin token |
| POST /admin/staff/{id}/invitation | expectedVersion | cuenta actualizada; sin token |
| POST /auth/staff/activate | token,password | activated; sin sesión |
| PATCH /admin/staff/{id}/role | role,expectedVersion | cuenta actualizada |
| PATCH /admin/staff/{id}/active | active,expectedVersion | cuenta actualizada |
| POST /admin/staff/{id}/revoke-sessions | expectedVersion | cuenta actualizada |
| GET /admin/staff/audit-events | after secuencia,limit 1–100 (20) | items metadatos saneados,next secuencia/null |

Roles modificables: Admission,Practitioner,Support. Estados: PendingActivation,Active,Suspended.
UUID textual canónico. Correo normalizado de hasta 254 caracteres. Contraseña 12–128 caracteres.
Versiones positivas; toda mutación/reenvío incrementa versión. Los filtros desconocidos se rechazan.
Errores: 400 VALIDATION_FAILED/INVALID_CHALLENGE; 401 SESSION_REQUIRED/INVALID_CREDENTIALS;
403 ACCESS_DENIED; 404 NOT_FOUND; 409 ACCOUNT_VERSION_CONFLICT/ACCOUNT_ALREADY_EXISTS/
ACCOUNT_TRANSITION_INVALID; 429 RATE_LIMITED.
Solo el administrador autorizado observa duplicados; endpoints públicos mantienen neutralidad.

## Activación y sesión

Invitación de 256 bits, hash persistido, uso único, 15 minutos. Reenvío invalida la anterior y
requiere 60 segundos desde la última emisión. Activación y reenvío comparten la política auth
existente de token bucket por IP: capacidad30, reposición de1 token cada2s, cola0 (tasa sostenida30 solicitudes/minuto). Se conserva bloqueo tras cinco fallos de
contraseña durante cinco minutos. No se añaden envíos SMTP para invitaciones.

El token solo está disponible en el buzón privado Development, autenticado con
X-Dev-Mailbox-Key y configuración privada Milestone:MailboxKey. Nunca lo devuelve una ruta
administrativa ni se incluye en logs, auditoría, outbox, URL o evidencias. La pantalla
/activar-personal recibe el token en un campo enmascarado, exclusivamente en memoria.
No pasar tokens por query strings.

Activación requiere invitación pendiente; no inicia sesión automáticamente. active=true
no activa una cuenta pendiente. Suspensión/cambio de rol revocan sesiones e invitaciones.
Reactivación no recupera tokens. Revocar sesiones deja permitido un nuevo login.
Cambio de rol pendiente requiere nueva invitación. No se borran cuentas.

Todas las escrituras protegidas revalidan la sesión después de bloquear al actor.
Administración bloquea actor/destinatario en orden estable. Login/refresh/logout y activación
siguen orden usuario antes de sesión/invitación. Una revocación observa también rotaciones
terminadas antes de adquirir el bloqueo. La vigencia absoluta de sesión no se amplía.
Auditoría y mutación comparten transacción; fallo de auditoría revierte la operación.
Los rechazos no confirman existencia de destinatarios. Auditoría de personal solo expone
sequence,id,actorId,action,resourceId,occurredAt,result,traceId. Eventos históricos: Legacy.

Portal mantiene cookies HttpOnly. Flutter conserva tokens solo en memoria, consume /auth/me,
rechaza personal y limpia datos privados al invalidarse sesión. 401 solicita autenticación;
403 comunica ausencia de permiso sin bucle de login.

## Bootstrap explícito

Migración 005 se ejecuta únicamente mediante la operación --migrate ya existente.
--seed-synthetic conserva el seed de Admission y ahora también valida Development internamente.
--seed-staff-synthetic valida Development y Milestone:Enabled dentro del seeder.
Requiere Milestone:AdministratorEmail/AdministratorPassword privados; opcionalmente pares
PractitionerEmail/PractitionerPassword y SupportEmail/SupportPassword.
No copiar valores a archivos versionados, CLI, capturas ni informes. El seed no cambia roles,
contraseñas ni estados de cuentas existentes. Una colisión de rol aborta.
El comando original de seed aporta catálogo y Admission; Patient puede registrarse/verificarse
sintéticamente. Los seeds no representan acreditación profesional.

## Verificación propuesta, no ejecutada

Después de revisar/aplicar el parche en un checkout autorizado y una BD sintética:
- Compilar API .NET 10 y ejecutar Telemedicina.Tests.
- Ejecutar Integration/second_milestone_e2e.py con MILESTONE_TEST_SYNTHETIC=1,
  Mail__Mode=development, credenciales privadas y API loopback.
- Ejecutar pruebas Flutter y análisis en app/src.
- Compilar portal y ejecutar Playwright con playwright.second.config.ts,
  MILESTONE_TEST_PHASE=second y MILESTONE_TEST_SYNTHETIC=1.
- Ejecutar regresión del primer hito con los mismos límites sintéticos.

No activar SMTP real en estas pruebas. No guardar storageState, HAR, trazas ni videos.
El runner HTTP conserva el límite real de autenticación; puede tardar varios minutos.
Las cuentas sintéticas creadas persisten y deben revisarse mediante el ciclo administrativo.

Pendientes de ejecución dirigida: vencimiento real de invitación/reenvío, fallo de auditoría
con rollback, segundo administrador como destinatario, pruebas de bloqueos bajo carga,
arranque migrado, inspección OpenAPI generado, accesibilidad, revisión PR y UAT.
PASS de formato del diff no equivale a compilación, aceptación funcional ni seguridad.
