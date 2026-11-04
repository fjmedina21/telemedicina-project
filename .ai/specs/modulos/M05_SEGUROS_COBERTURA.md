# M05 — Afiliación, cobertura y autorizaciones

**Prioridad:** P0 revisión manual; P1 integraciones automáticas · **Actores:** paciente, admisión, aseguradora/ARS, facturación · **Depende de:** M02, M03, M04

## Objetivo
Determinar elegibilidad de cobertura y necesidad de autorización antes de comprometer condiciones económicas de una cita.

## Funciones e historias
- US-16: Como paciente, registro aseguradora y referencia de afiliación sin publicar datos completos innecesariamente.
- US-17: Como admisión, consulto solicitudes pendientes y registro resultado documentado de revisión manual.
- US-18: Como paciente, conozco si una cita está `pendiente`, `aprobada`, `rechazada` o requiere atención particular.
- US-19 (P1): Como sistema, consulto ARS mediante conector con timeout/reintentos controlados.

## Pantallas Flutter
Seleccionar seguro; detalle de póliza; resultado de elegibilidad; autorización pendiente; alternativa particular si procede.

## Reglas
- Distinguir: identidad/afiliación vigente, cobertura para servicio y autorización específica.
- Nunca inferir aprobación por ausencia de respuesta del proveedor.
- Resultados se asocian a servicio y periodo de vigencia; no se reutilizan indiscriminadamente.
- Paciente puede optar por particular solo si existe política aprobada y condiciones transparentes.
- Integración/consultas y cambios de estatus auditados; evitar exponer credenciales ARS en cliente.

## Datos mínimos
`Insurer(id, name)`; `InsurancePolicy(id, patientId, insurerId, maskedMemberId, validTo?)`; `CoverageCheck(id, appointmentId, status, method, checkedAt, reviewerId?, evidenceRef?, validUntil?)`; `Authorization(id, checkId, reference, status, expiresAt?)`.

## API candidata
`GET /insurers`, `POST /patients/me/insurance-policies`, `POST /coverage-checks`, `GET /coverage-checks/{id}`; recursos de revisión de admisión protegidos.

## Excepciones
ARS sin servicio, afiliación no encontrada, póliza vencida, plan sin cobertura, autorización pendiente, cambio de condiciones, revisión manual caducada.

## Criterios de aceptación
1. Sin respuesta de ARS el estado es `ManualReview` o `Pending`, nunca `Approved`.
2. Admisión puede resolver manualmente con usuario, fecha y evidencia auditada.
3. Paciente recibe mensaje claro sobre cobertura sin información clínica en push.
4. Una aprobación caducada no se utiliza para nueva cita o nuevo servicio.
