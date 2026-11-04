# M07 — Videoconsulta y sala de espera

**Prioridad:** P0 · **Actores:** paciente, médico, soporte · **Depende de:** M01, M04, M06; SDK externo

## Objetivo
Conectar exclusivamente a paciente/tutor autorizado y profesional asignado, con buena experiencia de espera y reconexión.

## Funciones e historias
- US-25: Como paciente con cita apta, puedo entrar a sala de espera y unirme cuando médico habilite la consulta.
- US-26: Como médico, admito al paciente y veo el contexto mínimo de su cita.
- US-27: Como participante, silencio micrófono, desactivo cámara, compruebo red y recupero sesión interrumpida.
- US-28: Como soporte, veo métricas técnicas acotadas sin acceso al contenido clínico.

## Pantallas Flutter
Prellamada/permisos; sala de espera; videoconsulta; desconectado/reintentar; consulta finalizada; problemas de conexión.

## Reglas
- Backend valida cita confirmada, ventana temporal, check-in listo y rol antes de emitir token efímero del proveedor de video.
- Sala ligada a un `appointmentId`; token no contiene información clínica innecesaria y caduca.
- Grabar está deshabilitado por defecto; cualquier grabación futura requiere política y consentimiento explícitos.
- Una desconexión temporal no cierra automáticamente el encuentro clínico; el médico controla su cierre.
- No incluir URL/clave de sala permanente en notificaciones abiertas ni logs.

## Datos mínimos
`VideoSession(id, appointmentId, providerRoomId, status, startedAt?, endedAt?)`; `SessionParticipant`; `ConnectionEvent(type, occurredAt, nonSensitiveDiagnostic)`.

## API candidata
`POST /appointments/{id}/video-token`, `GET /appointments/{id}/video-status`, `POST /video-sessions/{id}/end`; webhooks validados del proveedor.

## Excepciones
Permisos mic/cámara rechazados, ancho de banda insuficiente, médico retrasado, token caducado, reconexión, proveedor caído, paciente duplicado en dos dispositivos.

## Criterios de aceptación
1. Un usuario ajeno a la cita jamás obtiene token de sala.
2. Si el token caduca, un paciente autorizado puede solicitar uno nuevo dentro de su ventana sin crear sesión duplicada.
3. Reconexión y fin de llamada muestran estado coherente; error externo ofrece ayuda y ruta operativa.
4. No se registra audio/video y la telemetría técnica no incluye datos clínicos.
