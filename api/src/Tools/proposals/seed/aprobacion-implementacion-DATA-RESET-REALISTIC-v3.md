# Autorización humana de implementación — DATA-RESET-REALISTIC-v3

- Spec: `api/src/Tools/proposals/seed/DATA-RESET-REALISTIC-v3.md`
- Decisión: autorizada la implementación de la migración versionada 009 que restaura únicamente `audit_no_mutation` y `history_no_mutation`, junto con ajustes de preflight, pruebas y documentación.
- Evidencia textual: «revisa las migraciones y buscas por los trigger faltantes y crealos».
- Límite: no autoriza aplicar la migración a la base remota configurada ni ejecutar el reset de datos.
- Fecha: 2026-11-06.

## Enmienda de alcance — migración remota

- Evidencia textual posterior: «apunta a la bd remoto».
- Decisión: autorizada la aplicación de migraciones al destino remoto actualmente configurado para crear/restaurar los dos triggers faltantes.
- Límite vigente: esta autorización no incluye `--reset-demo-data --execute`; el reemplazo de datos sigue sujeto a aprobación del preflight con conteos.
