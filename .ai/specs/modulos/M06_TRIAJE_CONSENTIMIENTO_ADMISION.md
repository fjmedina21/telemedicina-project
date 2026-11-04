# M06 — Triaje de admisibilidad, consentimiento y admisión virtual

**Prioridad:** P0 · **Actores:** paciente, tutor, admisión, médico · **Depende de:** M01, M02, M04, M05

## Objetivo
Comprobar si el paciente puede avanzar a consulta remota y documentar identificación, consentimiento informado y preparación técnica.

## Funciones e historias
- US-20: Como paciente, respondo preguntas de admisibilidad y recibo instrucciones cuando no corresponde continuar por teleconsulta.
- US-21: Como paciente, reviso y acepto consentimiento versionado antes de entrar a la sala.
- US-22: Como tutor, acepto cuando el paciente es menor y el vínculo autorizado está verificado.
- US-23: Como admisión, veo pendientes y puedo solicitar revisión clínica/manual.
- US-24: Como paciente, realizo prueba de cámara/micrófono/conectividad con ayuda.

## Pantallas Flutter
Preparar cita; cuestionario; verificación de identidad; texto de consentimiento; prueba técnica; admisión completada; derivación/instrucciones.

## Reglas
- Cuestionario y señalización de posible urgencia definidos por responsables clínicos; el software no diagnostica ni decide autónomamente una emergencia.
- Si se detecta un criterio de exclusión, bloquear flujo automático y mostrar instrucciones aprobadas de atención presencial/urgente.
- Guardar `consentVersion`, `patientId`, `acceptedBy`, `timestamp`, `appointmentId`, alcance y evidencia de aceptación.
- No habilitar `Ready` si falta identidad requerida, consentimiento o validaciones obligatorias.
- Check-in abierto solo en ventana configurable; manejo de negativa del consentimiento.

## Datos mínimos
`TriageTemplate`, `TriageResponse(appointmentId, answers, outcome, reviewedBy?)`; `ConsentDocument(id, version, scope, effectiveFrom)`; `ConsentAcceptance`; `CheckIn(appointmentId, status, verifiedAt?, readyAt?)`.

## API candidata
`GET /appointments/{id}/check-in-requirements`, `POST /appointments/{id}/triage`, `POST /appointments/{id}/consents`, `POST /appointments/{id}/check-in`, `GET /appointments/{id}/check-in`.

## Excepciones
Consentimiento rechazado, adulto/tutor inválido, ID no coincide, check-in fuera de ventana, preguntas omitidas, derivación presencial, dificultad técnica.

## Criterios de aceptación
1. Sin consentimiento vigente no se genera acceso a videollamada.
2. Negativa de consentimiento se registra y deriva al flujo alternativo autorizado.
3. Toda aceptación se puede reconstruir con documento exacto mostrado y actor.
4. Paciente recibe instrucciones claras si no debe usar el canal remoto; no recibe consejos clínicos generados por algoritmo.
5. Una nueva versión de consentimiento se presenta cuando el alcance de atención lo requiera.
