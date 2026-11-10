# H07-SPEC-v1 — Videoconsulta y sala de espera

**Estado:** aprobada para implementación el 2026-11-09.
**Base:** M07, `ARQUITECTURA_Y_CONTRATOS.md` y H06-SPEC-v2-DEMO.

## Proveedor de video propuesto

H07 utilizará **LiveKit Cloud** como proveedor inicial de medios en tiempo real.
La app Flutter integrará el paquete `livekit_client`; el portal utilizará el
cliente web de LiveKit. La API ASP.NET Core seguirá siendo el único emisor de
tokens, después de validar la cita, el check-in, el rol y la ventana temporal.

LiveKit soporta salas con identidad de participante y token, publicación de
audio/video, reconexión y diagnóstico de conectividad en Flutter. Su arquitectura
permite mantener el contrato de la aplicación independiente del proveedor y
considerar una instalación propia en una fase posterior. La aprobación de H07
debe incluir la revisión de cuenta, región, costos, privacidad, retención y
condiciones contractuales de LiveKit Cloud.

## Contrato funcional

Una sesión pertenece a una sola cita. El paciente solo puede solicitar o renovar
un token para su propia cita; el profesional solo puede operar la cita cuyo
`practitioner_id` coincide con su identidad. `Admission` y `Support` no obtienen
tokens de participante.

Condiciones para crear o renovar una sesión:

- la cita existe y está en `Confirmed`;
- el check-in actual está en `Ready` y corresponde a la versión de la cita y del perfil;
- la hora actual está dentro de la ventana aprobada para H07;
- el actor tiene la relación autorizada con la cita;
- la sesión no está `Ended` ni `Failed`.

El servidor y el proveedor determinan el resultado de conexión. Flutter no puede
declarar `Active`, `Ended` ni autorización de acceso por cuenta propia.

## Estados

```text
Created → Waiting → Active → Ended
             ↓         ↓
        Interrupted ←──┘

Created/Waiting/Interrupted → Failed
```

`Interrupted` es recuperable mientras el token y la autorización sigan vigentes.
Salir de la pantalla no finaliza la sesión clínica; el profesional puede finalizar
la sala y el backend registra el cierre técnico.

## API propuesta

Todas las rutas requieren sesión autenticada y respuesta `problem+json` en errores.

```text
POST /api/v1/appointments/{id}/video-token
GET  /api/v1/appointments/{id}/video-status
POST /api/v1/video-sessions/{id}/admit
POST /api/v1/video-sessions/{id}/end
POST /api/v1/video-sessions/{id}/connection-events
POST /api/v1/video/webhooks/{provider}
```

`POST video-token` recibe el rol de uso y una clave `Idempotency-Key` UUID.
Devuelve únicamente un token efímero, expiración, identificador opaco de sesión,
configuración mínima del SDK y estado; nunca devuelve una URL permanente ni datos
clínicos.

`GET video-status` devuelve estado, participantes presentes de forma agregada,
expiración y códigos técnicos no sensibles. No devuelve declaraciones de H06,
consentimiento, notas ni contenido de la cita más allá del mínimo operativo.

`admit` queda restringido al profesional asignado. `end` queda restringido al
profesional asignado o a una operación de soporte explícitamente auditada; el
paciente puede abandonar la conexión sin cerrar el encuentro.

Los webhooks validan firma, timestamp, idempotencia y proveedor configurado antes
de mutar una sesión. Un evento desconocido o repetido no debe reabrir una sesión.

## Persistencia propuesta

- `video_sessions`: sesión única activa por cita, proveedor, room reference opaca,
  estado, timestamps, versión y auditoría mínima.
- `video_session_participants`: actor, rol, entrada, salida y estado técnico.
- `video_connection_events`: tipo, momento UTC, código diagnóstico permitido y actor;
  sin payload libre ni identificadores de medios.
- `idempotency`: reutilizar el mecanismo existente con hash de solicitud y sujeto.

El token crudo, credenciales del proveedor y secretos de webhook no se almacenan
en PostgreSQL, logs, Flutter ni portal. La referencia de sala debe ser opaca y no
debe aparecer en notificaciones abiertas.

## Flutter y portal

Flutter tendrá estados explícitos para prellamada, permisos, sala de espera,
conectando, conectado, interrumpido, reintentando, finalizado y error. Las
permisiones de cámara/micrófono se solicitan justo antes de unirse; rechazarlas
permite continuar con una pantalla de ayuda, no concede acceso silencioso.

El portal mostrará al profesional solo sus citas aptas y el estado operativo de la
sala. No mostrará H06 completo ni contenido clínico, y no permitirá admitir una
cita de otro profesional.

El prototipo actual de `app/src/lib/prototypes/video_visit/` no se considera
implementación válida: no debe afirmar cifrado extremo a extremo, conexión real,
grabación deshabilitada por proveedor ni consulta terminada hasta tener evidencia.

## Criterios verificables

1. Paciente ajeno, profesional no asignado, `Admission` y `Support` reciben `403`
   o `404` indistinguible según la política de enumeración y nunca un token.
2. Cita no confirmada, check-in no `Ready`, ventana cerrada o cita cancelada no
   crean ni reactivan una sesión.
3. Dos solicitudes concurrentes con la misma cita producen una sola sesión activa;
   el replay idempotente no entrega un token histórico inválido.
4. Token expirado puede renovarse solo con autorización vigente y sin duplicar la
   sala ni ampliar la ventana.
5. Webhooks inválidos, repetidos o fuera de orden no alteran indebidamente el estado.
6. Reconexión e interrupción muestran estados coherentes y no cierran el encuentro.
7. No se registran audio/video, tokens, secretos, URLs permanentes ni datos clínicos
   en logs, eventos de conexión, notificaciones o telemetría.
8. Se distinguen pruebas ejecutadas de pruebas pendientes en el reporte Validator.

## Riesgo bloqueante

LiveKit Cloud queda como proveedor recomendado, pero no se debe añadir el SDK,
crear credenciales, publicar una conexión de video ni afirmar funcionamiento de
producción hasta que la persona responsable apruebe esta especificación y las
condiciones de privacidad/seguridad del proveedor.
