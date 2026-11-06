# Handoff de revisión — DATA-RESET-REALISTIC-v4

**Fecha:** 2026-11-06  
**Architect:** recomendación de permitir solo el destino remoto exacto después de aprobación de spec.  
**Spec Creator:** propuesta para guard allowlist remoto con segunda aprobación del informe.  
**Estado:** pendiente de aprobación humana.

## Estado confirmado

- Migración 009 aplicada al Aiven remoto configurado; código de salida 0.
- Preflight read-only posterior pasó con schema versions 1–9 y los tres triggers esperados activos.
- El dry-run devolvió `committed=false` y `after=null`; no se cambiaron filas.
- Destino reportado: `pg-2db5ca83-telemedicina-iso910.l.aivencloud.com:10036/defaultdb`.
- Guard actual `RequireLocalTarget` rechaza destino remoto en `--execute`.

## Impacto del reset reportado

Antes: 105 users, 34 profiles, 127 sessions, 52 challenges, 8 dev_mailbox, 35 services,
35 practitioners, 1226 slots, **250 appointments**, **290 appointment_history**, 48 idempotency,
**834 audit**, 47 practitioner_services, 17 notification_preferences, 92 notifications,
87 email_outbox y 27 staff_invitations.

Previsto después: users 30, profiles 24, services 6, practitioners 6, slots 800 (60 bloqueados),
appointments 0, appointment_history 0, notifications 0, notification_preferences 24,
audit 1; las demás tablas del manifiesto en cero.

Los conteos no revelan si las filas son sintéticas. No hubo respaldo automático ni reset.

## Recomendación y puerta pendiente

La propuesta v4 permite, si se aprueba, cambiar el guard únicamente para el host, puerto y base exactos del reporte. Mantiene constraints y `appointment_history_written` activos; el reset solo deshabilita temporalmente los dos guards append-only dentro de la transacción. Requiere aprobación de v4 antes del cambio de guard y aprobación separada del informe antes de `--execute`.

La petición de desactivar todas las protecciones no se incorporó porque debilita integridad y auditoría. La instrucción de apuntar a la base remota se aplicó solo a la migración correctiva de triggers; no autoriza borrar las filas existentes.
