# M04 — Citas, disponibilidad y agendamiento

**Prioridad:** P0 · **Actores:** paciente, admisión, médico · **Depende de:** M01, M02, M03; integración con M05/M06

## Objetivo
Gestionar el ciclo de cita desde solicitud hasta consulta, cancelación, reprogramación o no asistencia.

## Funciones e historias
- US-12: Como paciente, selecciono servicio, médico, fecha, horario, modalidad de pago/cobertura y motivo breve.
- US-13: Como paciente, puedo consultar el estado, cancelar o pedir reprogramación según política.
- US-14: Como médico, veo agenda de citas confirmadas y cambios relevantes.
- US-15: Como admisión, registro/confirmo citas solicitadas por teléfono, correo o WhatsApp, centralizando una sola agenda.

## Pantallas Flutter
Selección de horario; resumen y confirmación; Mis citas (próximas/históricas); detalle; cancelar; reprogramar.

## Reglas
- Prohibir dos citas que solapen para el mismo médico; resolver concurrencia en DB mediante transacción/restricción, no solo en UI.
- Política de anticipación, reprogramación, cancelación y no-show configurable por servicio.
- Estados base `Requested`, `PendingValidation`, `Confirmed`, `Completed`, `Cancelled`, `Rejected`, `NoShow`.
- Reprogramación crea evento histórico y aviso de nueva hora; conservar referencia y auditoría.
- Emisión de cita confirmada depende de cobertura/pago y validación, cuando corresponda; no confundir 'solicitada' con 'confirmada'.
- `Idempotency-Key` en creación; repetir request con la misma llave no produce segunda cita.

## Datos mínimos
`Appointment(id, patientId, practitionerId, serviceId, startAtUtc, endAtUtc, status, reasonSummary?, coverageCheckId?, createdBy, version)`; `AppointmentHistory`; `SlotHold` opcional con TTL.

## API candidata
`POST /appointments`, `GET /appointments/me`, `GET /appointments/{id}`, `POST /appointments/{id}/cancel`, `POST /appointments/{id}/reschedule`; endpoint de agenda para médicos.

## Excepciones
Slot tomado entre lectura y reserva (`409`), fecha inválida, cobertura pendiente, límite de reprogramaciones, médico cancela, paciente no llega.

## Criterios de aceptación
1. Dos usuarios reservando simultáneamente el mismo slot: como máximo una reserva confirmada.
2. Cancelación autorizada libera capacidad y envía evento una sola vez.
3. Toda cita muestra estado consistente y zona horaria local.
4. Cancelación y reprogramación no borran historial.
5. Una solicitud pendiente no habilita sesión de video hasta completar condiciones de admisión.
