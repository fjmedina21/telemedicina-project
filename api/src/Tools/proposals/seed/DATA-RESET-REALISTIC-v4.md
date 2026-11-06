# Spec revisable: reset realista al destino remoto aprobado

**Versión:** `DATA-RESET-REALISTIC-v4`  
**Estado:** aprobada para implementar la guarda; el reset sigue pendiente de aprobación del informe.  
**Base:** seed `SEED-REALISTA-v2` y migración 009 ya aplicada en el destino remoto.  
**Motivo:** el destino de la previsualización es PostgreSQL remoto y el guard de ejecución actual solo permite loopback. Esta spec propone habilitar exclusivamente el destino reportado, sin ampliar a otros servidores.

## Destino exacto permitido

La única base remota a la que esta propuesta permitiría `--execute` es:

| Campo | Valor esperado |
|---|---|
| Proveedor/entorno | Aiven, Development solicitado por el usuario |
| Host | `pg-2db5ca83-telemedicina-iso910.l.aivencloud.com` |
| Puerto | `10036` |
| Base | `defaultdb` |
| Esquema | `telemedicina_m1` |
| Versiones de esquema | `1–9` |

La guarda de `--execute` debe exigir coincidencia exacta de host, puerto y nombre de base con esta allowlist, además de los guardas de entorno actuales. Cualquier otro host, puerto o base debe abortar antes de escribir. Un nombre de configuración que el operador pueda cambiar no debe permitir ampliar por sí solo la allowlist.

## Informe de dry-run observado

La migración 009 se aplicó con código de salida 0. El preflight posterior pasó con los tres triggers esperados habilitados. El dry-run devolvió `committed=false`, `after=null` y no modificó filas.

Conteos actuales observados:

| Tabla | Actual | Previsto |
|---|---:|---:|
| users | 105 | 30 |
| profiles | 34 | 24 |
| sessions | 127 | 0 |
| challenges | 52 | 0 |
| dev_mailbox | 8 | 0 |
| services | 35 | 6 |
| practitioners | 35 | 6 |
| slots | 1226 | 800 |
| appointments | **250** | **0** |
| appointment_history | **290** | **0** |
| idempotency | 48 | 0 |
| audit | **834** | 1 |
| practitioner_services | 47 | 6 |
| notification_preferences | 17 | 24 |
| notifications | 92 | 0 |
| email_outbox | 87 | 0 |
| staff_invitations | 27 | 0 |

El reset reemplazaría todas las filas de las 17 tablas listadas. No hay respaldo automático ni rollback después del commit. El conteo agregado no demuestra que el contenido sea sintético. No consultar ni exponer contenido de pacientes para decidirlo; la persona responsable debe confirmar que el destino es de desarrollo y que esos datos se pueden reemplazar.

## Cambios de implementación propuestos

- Sustituir `RequireLocalTarget` por una guarda de destino que admita loopback según la política actual y, como única excepción remota, el host/puerto/base indicados arriba.
- Requerir una configuración explícita de confirmación del destino remoto con host, puerto y base coincidentes. No admitir comodines, sufijos de host, puertos alternos ni `Database` dinámico.
- Mantener preflight estricto de manifiesto, versiones 1–9, propietario, referencias externas y los tres triggers exactos y habilitados.
- Mantener constraints, claves foráneas, exclusiones de agenda y trigger `appointment_history_written` activos.
- Durante el reset, deshabilitar únicamente `audit_no_mutation` y `history_no_mutation` dentro de la misma transacción para `TRUNCATE`; reactivarlos antes de insertar. No deshabilitar todas las protecciones ni usar `CASCADE`.
- Mantener dataset v2: 30 usuarios, 24 perfiles, 6 servicios y 6 profesionales, 800 slots/60 bloqueados, 24 preferencias, cero citas, historial y notificaciones.

## Puertas de aprobación

1. Obtener aprobación explícita de `DATA-RESET-REALISTIC-v4` antes de modificar el guard de ejecución.
2. Compilar API y pruebas unitarias; ejecutar las validaciones correspondientes al cambio.
3. Verificar estáticamente que solo se permite el endpoint exacto de la allowlist y que la configuración no amplía el destino.
4. Correr de nuevo el dry-run sin `--execute` y presentar destino y conteos actuales/previstos.
5. Obtener una segunda aprobación explícita de ese informe, incluida la aceptación de reemplazar 250 citas, 290 filas de historial y 834 eventos de auditoría.
6. Solo después ejecutar `--execute` y comprobar los conteos finales y que schema version 9 y los tres triggers siguen activos.

La spec fue aprobada para implementar la guarda de destino remoto exacto. No constituye aprobación para borrar datos del destino; ese paso requiere aprobación posterior del informe de preflight.
