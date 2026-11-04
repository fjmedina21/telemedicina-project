# M08 — Encuentro clínico e historial

**Prioridad:** P0 encuentro básico; P1 historia longitudinal ampliada · **Actores:** médico, paciente (lectura restringida), supervisor clínico · **Depende de:** M02, M04, M07

## Objetivo
Documentar la atención médica, organizar notas y garantizar integridad, autoría, trazabilidad y acceso clínico legítimo.

## Funciones e historias
- US-29: Como médico asignado, consulto antecedentes/autodeclaraciones relevantes autorizadas antes y durante la atención.
- US-30: Como médico, registro motivo, anamnesis, hallazgos, evaluación, indicaciones y plan.
- US-31: Como médico, firmo/cierro el encuentro bajo una política de firma aprobada.
- US-32: Como paciente, accedo al resumen publicado de la consulta, no a notas internas no publicadas.

## Portal médico
Agenda del día; ficha mínima; formulario clínico; guardado borrador; revisión; firmar/cerrar; historial accesible según relación asistencial.

## Reglas
- Solo médico asignado o suplente autorizado edita notas del encuentro.
- Estados `Draft`, `InProgress`, `PendingSignature`, `Signed`, `Closed`.
- Notas firmadas son inmutables: corrección como addendum nuevo, firmado y auditado.
- Campos sensibles y acceso a registros específicos se filtran por necesidad de atención y política del centro.
- El consentimiento de consulta no implica automáticamente permiso para compartir expediente con terceros.

## Datos mínimos
`Encounter(id, appointmentId, patientId, practitionerId, status, startedAt?, endedAt?)`; `ClinicalNote(id, encounterId, section, content, version, authorId, signedAt?)`; `ClinicalAddendum`; `Observation`; `EncounterAccessGrant`.

## API candidata
`GET /encounters/{id}`, `POST/PATCH /encounters/{id}/notes`, `POST /encounters/{id}/sign`, `POST /encounters/{id}/addenda`, `GET /patients/{id}/encounters` (profesional autorizado).

## Excepciones
Médico sin autorización, pérdida de red durante edición, conflicto de versión, firma pendiente, abandono de consulta, información autodeclarada incorrecta.

## Criterios de aceptación
1. Dos ediciones concurrentes no sobrescriben datos silenciosamente (`409`/control de versión).
2. Un encuentro firmado no se modifica; queda addendum trazable.
3. Médico no asignado no puede consultar notas salvo excepción autorizada registrada.
4. El resumen de paciente se publica solo al completar reglas de cierre y autorización.
