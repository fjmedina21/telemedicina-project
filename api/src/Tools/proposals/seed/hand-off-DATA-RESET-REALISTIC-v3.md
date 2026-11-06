# Handoff de revisión — DATA-RESET-REALISTIC-v3

**Fecha:** 2026-11-06  
**Architect:** recomendación recibida en esta ejecución.  
**Estado:** migración 009 aplicada al destino remoto autorizado; reset pendiente de spec remota y aprobación del reporte.

## Evidencia

El primer preflight read-only reportó solo `appointments.appointment_history_written`, habilitado y ligado a `telemedicina_m1.track_appointment`; faltaban los guards append-only. La migración 009 se aplicó después con código de salida 0. El segundo preflight pasó con versiones 1–9 y los tres triggers esperados habilitados.

Destino saneado reportado: `pg-2db5ca83-telemedicina-iso910.l.aivencloud.com:10036/defaultdb` (Aiven). No se ejecutó `--execute` ni se modificaron filas de negocio.

Informe de previsualización: `committed=false`, `after=null`. Conteos antes: users 105, profiles 34, sessions 127, challenges 52, dev_mailbox 8, services 35, practitioners 35, slots 1226, appointments 250, appointment_history 290, idempotency 48, audit 834, practitioner_services 47, notification_preferences 17, notifications 92, email_outbox 87, staff_invitations 27. Conteos previstos: users 30, profiles 24, services 6, practitioners 6, slots 800 (60 bloqueados), appointments 0, appointment_history 0, notifications 0, notification_preferences 24; las demás tablas quedan en cero salvo 1 evento de auditoría del reset.

## Decisión de diseño propuesta

Se restauraron únicamente los dos triggers faltantes mediante migración aditiva versionada 009. El trigger de historial y demás protecciones siguen activos. El siguiente paso destructivo requerirá una spec que permita específicamente el destino remoto, una guarda de host/base exactos y aprobación humana del reporte de conteos. El seed conserva la segunda puerta previa al reset.

La petición de desactivar todas las protecciones no se incorpora: no repara los triggers ausentes, debilita auditoría e historial y contradice el preflight y alcance ya aprobados en v2.

## Validaciones globales observadas en la misma ejecución

- API Release build: pasó, 0 warnings.
- API unit checks: 34 pasaron, 0 fallaron.
- Flutter analyze: 15 hallazgos informativos/deprecaciones; sin errores de análisis reportados.
- Flutter tests: 48 pasaron.
- Portal Next.js production build: pasó.
- Portal unit checks de sesión: 3 pasaron.
- Portal lint no pudo completarse: `next lint` abrió el asistente interactivo porque no hay configuración ESLint definida.
- No se ejecutaron pruebas E2E/DB que escriben en una base compartida.
