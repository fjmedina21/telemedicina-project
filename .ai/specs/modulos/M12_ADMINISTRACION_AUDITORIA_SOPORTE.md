# M12 — Administración, auditoría, soporte y métricas

**Prioridad:** P0 mínimo operativo; P1 analítica y automatización avanzada · **Actores:** admisión, coordinador, soporte, auditor, administrador · **Depende de:** todos los dominios

## Objetivo
Permitir que una organización opere la telemedicina, gestione incidencias y rastree acciones críticas con segregación de funciones.

## Funciones e historias
- US-45: Como admisión, reviso citas pendientes, cobertura manual y check-ins incompletos.
- US-46: Como coordinador, habilito servicios, asigno profesionales y bloqueo horarios.
- US-47: Como soporte, reviso fallos de conexión y ayudo sin acceso injustificado a notas clínicas.
- US-48: Como auditor, consulto quién accedió o modificó un recurso clínico y cuándo.
- US-49 (P1): Como gestor, veo métricas agregadas de acceso, tiempos, completitud y satisfacción.

## Pantallas portal web
Panel operativo; cola de validación; agenda; gestión de servicios/profesionales; incidentes; auditoría filtrable; métricas agregadas.

## Reglas
- Separación de roles: soporte técnico no equivale a clínico; facturación no equivale a acceso total al expediente.
- Cambios administrativos, exportaciones y accesos sensibles quedan auditados de forma resistente a alteraciones.
- Acceso de emergencia a expediente (break-glass), si se incorpora, exige motivo, temporalidad y revisión posterior; no habilitar por defecto.
- Retención, anonimización y eliminación deben definirse con responsables legales y clínicos.
- Soporte no ve contenido de video ni documentos por defecto.

## Datos mínimos
`Organization`, `UserRoleAssignment`, `ServiceConfiguration`, `AuditEvent(id, actorId, action, resource, result, timestamp, correlationId)`, `SupportIncident`, `SystemSetting`, `MetricAggregate` (P1).

## API candidata
`GET /admin/work-queue`, `PATCH /admin/appointments/{id}/validation`, `POST /admin/schedules/blocks`, `GET /admin/audit-events`, `POST /support/incidents`, `GET /admin/metrics` (P1).

## Excepciones
Auditoría caída, admin sin permisos, modificación concurrente, incidencia urgente, exportación excesiva de datos, integraciones degradadas.

## Criterios de aceptación
1. Roles técnicos no pueden leer notas clínicas solo por administrar infraestructura.
2. Cambiar resultado de cobertura o cita crea evento de auditoría recuperable.
3. Panel operativo ofrece colas con estado real y acceso a la acción permitida.
4. Ante fallo de integración, puede registrarse incidencia sin falsear validación exitosa.
