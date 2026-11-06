# Autorización humana de implementación — DATA-RESET-REALISTIC-v4

- Spec aprobada: `api/src/Tools/proposals/seed/DATA-RESET-REALISTIC-v4.md`
- Handoff: `api/src/Tools/proposals/seed/hand-off-DATA-RESET-REALISTIC-v4.md`
- Decisión: aprobada la implementación del guard de destino para conservar loopback y
  permitir únicamente el endpoint Aiven exacto con confirmación configurada de host,
  puerto y base.
- Alcance: parche del código, pruebas unitarias y documentación; no incluye ejecución de
  `--reset-demo-data --execute`.
- Límite: el reset requiere revisar un nuevo dry-run y obtener aprobación explícita
  separada de sus conteos y destino.
- Evidencia textual: «apruebo DATA-RESET-REALISTIC-v4».
- Fecha: 2026-11-06.
