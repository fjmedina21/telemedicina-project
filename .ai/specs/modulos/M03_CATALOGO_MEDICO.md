# M03 — Catálogo clínico y disponibilidad

**Prioridad:** P0 · **Actores:** paciente, admisión, coordinador, médico · **Depende de:** M01

## Objetivo
Mostrar servicios de telemedicina habilitados y profesionales disponibles, con precios, modalidad y restricciones transparentes.

## Funciones e historias
- US-09: Como paciente, filtro servicios por especialidad y tipo de atención (primera visita/seguimiento/segunda opinión).
- US-10: Como paciente, veo disponibilidad y duración de consulta antes de seleccionar.
- US-11: Como coordinador, configuro agendas, duración, bloqueos y cupos sin duplicar horas.

## Pantallas Flutter
Inicio/buscador; categorías; detalle de servicio; detalle del médico; calendario y horarios.

## Reglas
- Mostrar solo servicios aprobados para atención remota; incluir aviso de límites y alternativa presencial.
- Disponibilidad generada en servidor según zona horaria, pausas, feriados y reservas vigentes.
- Disponibilidad es informativa hasta la confirmación atómica de M04; un slot puede agotarse.
- Especialidad no implica que todos sus procedimientos se puedan hacer virtualmente.

## Datos mínimos
`Specialty(id, name, active)`; `Service(id, specialtyId, name, durationMinutes, mode, active, eligibilityRules)`; `Practitioner(id, licenseRef?, displayName, status)`; `PractitionerService`; `ScheduleRule`; `BlockedTime`.

## API candidata
`GET /specialties`, `GET /services`, `GET /practitioners`, `GET /practitioners/{id}`, `GET /availability`, endpoints admin para horarios.

## Excepciones
Sin disponibilidad, profesional deshabilitado, servicio temporalmente suspendido, horarios cambiados, conflicto de zona horaria.

## Criterios de aceptación
1. El paciente solo ve horarios futuros de profesionales activos.
2. El calendario respeta hora local en visualización y UTC en almacenamiento, incluso con cambios de configuración.
3. Un horario bloqueado no aparece ni puede reservarse en M04.
4. Catálogo vacío explica opciones sin producir error técnico.
