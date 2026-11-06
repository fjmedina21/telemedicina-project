# Spec revisable: restaurar protecciones y completar el seed realista sin citas

**Versión:** `DATA-RESET-REALISTIC-v3`  
**Estado:** reemplazada por `DATA-RESET-REALISTIC-v4` para el reset remoto. La migración 009 fue aplicada en el destino descrito en el handoff; el reset no se ejecutó.  
**Base:** `DATA-RESET-REALISTIC-v2`, aprobada para preparar el seed sin citas y ejecutar solo preflight de lectura.  
**Hallazgo que motiva esta revisión:** el preflight de solo lectura encontró un solo trigger en `telemedicina_m1`: `appointments.appointment_history_written` (habilitado). Faltan `audit_no_mutation` y `history_no_mutation`. No se contaron ni modificaron datos.

## Objetivo

Restaurar los dos triggers append-only que faltan mediante una migración aditiva versionada y volver a ejecutar el preflight del seed `SEED-REALISTA-v2`. El seed debe seguir produciendo cero citas, historial y notificaciones.

## Alcance

- Agregar migración `009_restore_audit_history_guards.sql` para crear los triggers `audit_no_mutation` en `audit` y `history_no_mutation` en `appointment_history`, usando la función `telemedicina_m1.protect_audit` existente.
- Preservar `appointment_history_written` en `appointments`, habilitado y sin cambios.
- Si falta la función esperada o existe un trigger con definición inesperada, la migración aborta. No elimina triggers desconocidos ni reemplaza definiciones diferentes en silencio.
- Registrar la versión 9 de manera idempotente y actualizar el preflight, checks y documentación para exigir exactamente las versiones 1–9 y los tres triggers esperados.
- Conservar constraints, claves foráneas, exclusiones de agenda, otros triggers, esquemas, datos y fixtures. No desactivar todas las protecciones.
- Mantener `DemoDataPlan`/`DemoDataReset` v2: 30 cuentas, 24 perfiles, 6 servicios/profesionales, 800 slots/60 bloqueados, cero citas/historial/notificaciones.

## Ejecución y puertas de aprobación

La instrucción directa «revisa las migraciones y buscas por los trigger faltantes y crealos» autorizó implementar la migración y sus checks. La instrucción posterior «apunta a la bd remoto» autorizó aplicar las migraciones al destino remoto configurado, únicamente para restaurar los dos triggers faltantes. La migración 009 ya se aplicó; no modificó filas. La ejecución del reset remoto se separa en la spec v4 y sigue sin aprobación.

1. Implementar la migración y los checks; compilar y ejecutar las pruebas unitarias.
2. La migración 009 se aplicó al destino remoto exacto del handoff; confirmar que `schema_versions` es 1–9 y los tres triggers están activos.
3. Ejecutar `--reset-demo-data` sin `--execute`, obtener destino saneado y conteos. Si cualquier guarda falla, abortar.
4. El control de ejecución remota y la decisión de reset se especifican en `DATA-RESET-REALISTIC-v4`; no ejecutar `--execute` bajo esta v3.

La migración 009 es una reparación de esquema y no borra ni transforma filas. El reset posterior sí reemplaza datos de 17 tablas y no tiene reversión posterior al commit; por eso conserva la puerta separada de aprobación del reporte.

## Aceptación

- Antes de la migración, el diagnóstico solo detecta el trigger de historial y no expone credenciales ni datos personales.
- La migración agrega exclusivamente los dos guards faltantes y registra versión 9; si encuentra una definición inesperada, falla sin cambios parciales.
- Después de la migración, el preflight verifica los tres triggers activos y las versiones 1–9.
- El dry-run no escribe y presenta el destino y conteos saneados.
- No se desactiva todo el sistema de protección ni se relajan las guardas para forzar el seed.
- El reset final queda sujeto a aprobación explícita posterior al informe; previsto y posterior: citas, historial y notificaciones en cero.

## Límite de la aprobación

La aprobación previa de `DATA-RESET-REALISTIC-v2` no cubría la migración 009 ni el cambio del requisito de versiones 1–8 a 1–9. La instrucción posterior autorizó y permitió aplicar esa migración al remoto exacto. El reset irreversible requiere la revisión y aprobación posterior definidas en v4.
