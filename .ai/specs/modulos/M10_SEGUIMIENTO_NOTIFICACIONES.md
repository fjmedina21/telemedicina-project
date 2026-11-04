# M10 — Seguimiento, recordatorios, notificaciones y encuestas

**Prioridad:** P0 confirmaciones, recordatorios y postconsulta simple; P1 planes complejos · **Actores:** paciente, médico, admisión, sistema · **Depende de:** M04, M08, M09

## Objetivo
Mantener al paciente informado y posibilitar continuidad asistencial sin convertir notificaciones genéricas en intervenciones clínicas automáticas.

## Funciones e historias
- US-37: Como paciente, recibo confirmación y recordatorios de citas por canal elegido.
- US-38: Como paciente, consulto tareas de seguimiento y próxima cita desde la app.
- US-39: Como paciente, respondo encuesta breve de satisfacción.
- US-40: Como equipo asistencial, programo controles y reviso pendientes dentro de un flujo asignado.

## Pantallas Flutter
Centro de notificaciones; preferencias de canal; seguimiento; próximas acciones; encuesta posterior.

## Reglas
- Push/email/WhatsApp solo con texto neutro y enlace autenticado, sin diagnóstico, receta ni información sensible.
- Respetar permisos, opt-in/opt-out según canal y notificaciones que sean imprescindibles para la operación.
- Jobs idempotentes con plantilla/versionado, deduplicación y zona horaria local para enviar en ventanas autorizadas.
- Mensajes clínicamente significativos deben ser aprobados por personal autorizado; nunca diagnosticar mediante notificación.
- Encuestas no alteran expediente clínico; datos para calidad se usan con minimización.

## Datos mínimos
`Notification(id, recipientId, type, channel, status, sentAt?, relatedResourceId?)`; `NotificationPreference`; `CareTask(id, patientId, encounterId, dueAt, status, assignedTo?)`; `Survey`; `SurveyResponse`.

## API candidata
`GET /notifications/me`, `POST /notifications/{id}/read`, `GET/PATCH /notifications/preferences`, `GET /follow-ups/me`, `POST /surveys/{id}/responses`; endpoints privados de programación.

## Excepciones
Token push vencido, correo rebotado, WhatsApp no habilitado, doble disparo de worker, tarea vencida, cancelación después de envío.

## Criterios de aceptación
1. Crear la misma notificación con el mismo evento-id no genera dos envíos.
2. Cancelar una cita impide futuros recordatorios de esa cita.
3. Las notificaciones no exponen motivos de consulta ni diagnósticos en pantalla bloqueada.
4. Paciente ve historial pertinente y controla preferencias permitidas.
