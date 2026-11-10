# H07 — Plan de ejecución propuesto

**Estado:** aprobado para implementación el 2026-11-09; el parche queda sujeto a validación.

**Proveedor recomendado:** LiveKit Cloud, con `livekit_client` para Flutter y
cliente web LiveKit para el portal. La selección queda propuesta para aprobación;
la integración real aún no está implementada.

## Objetivo

Definir e implementar la base segura de videoconsulta para una cita confirmada,
con sala de espera, admisión por el profesional asignado, token efímero,
reconexión controlada y telemetría técnica sin contenido clínico.

## Alcance P0 propuesto

- API de sesión de video vinculada a `appointmentId`.
- Autorización exclusiva para el paciente titular y el profesional asignado.
- Requisito de cita `Confirmed`, check-in H06 `Ready` y ventana temporal vigente.
- Token efímero emitido por un adaptador de proveedor; nunca se persiste el token crudo.
- Estados `Created`, `Waiting`, `Active`, `Interrupted`, `Ended` y `Failed`.
- Renovación de token dentro de la autorización vigente sin crear sesiones duplicadas.
- Sala de espera y controles básicos de micrófono/cámara en Flutter mediante el SDK aprobado.
- Vista operativa mínima para que el profesional admita o finalice la sesión.
- Eventos técnicos acotados: conexión, reconexión, fallo, expiración y salida.
- Auditoría de acciones y errores sin audio, video, texto clínico, tokens ni URLs permanentes.
- Pruebas de aislamiento horizontal, expiración, concurrencia, replay e invalidación.

## Fuera de alcance

- Grabación, transcripción, almacenamiento de medios o captura de pantalla.
- Diagnóstico, triaje, decisiones clínicas o documentación del encuentro M08.
- Recetas, documentos, pagos, notificaciones clínicas o consentimiento legal.
- Uso con pacientes reales o afirmación de cumplimiento regulatorio.
- Selección definitiva de proveedor sin evaluación de privacidad, seguridad, costos,
  residencia de datos, SDK Flutter y webhooks.

## Áreas y responsables propuestos

- `fullstack`, responsable `backend_mobile`: contratos, persistencia, API y coordinación del adaptador.
- `frontend`, ambos colaboradores Flutter: sala de espera, permisos, reconexión y consumo de API/SDK.
- `portal`, colaborador autorizado: control mínimo del profesional asignado; revisión cruzada por PR.

## Dependencias y decisiones abiertas

1. Aprobar LiveKit Cloud como proveedor inicial y sus condiciones de privacidad,
   seguridad, costos, región y retención.
2. Confirmar si el profesional controla la admisión desde el portal actual o desde una interfaz adicional.
3. Aprobar política de permisos de cámara/micrófono y mensaje de privacidad.
4. Definir límites de ventana para entrar, renovar y finalizar la sesión.
5. Confirmar el proveedor de webhooks y su mecanismo de firma/replay protection.

## Puerta de aprobación

La implementación debe detenerse hasta que la persona responsable apruebe una versión concreta
de la especificación H07 y queden registradas las decisiones del proveedor y de permisos.
