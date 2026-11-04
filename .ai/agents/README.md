# Equipo de agentes — Telemedicina

El `team.json` fija el orden del pipeline y el máximo de pasos por rol. El CLI ejecuta estos agentes de forma **secuencial**, con handoff de resultados; no requiere servidores adicionales ni orquestadores propietarios.

| Orden | Archivo | Trabajo principal | Puede proponer patch |
|---|---|---|---|
| 1 | `ORCHESTRATOR.md` | Definir alcance, aceptación, dependencias | No |
| 2 | `ARCHITECT.md` | Diseñar componentes, datos, APIs y seguridad | No |
| 3 | `SPEC_CREATOR.md` | Refinar historias y pruebas verificables | No |
| 4 | `IMPLEMENTER.md` | Inspeccionar código y proponer implementación | Sí, un único patch |
| 5 | `VALIDATOR.md` | Revisar propuesta, riesgos y pruebas pendientes | No |

## Ejemplo

```bash
PYTHONPATH=.ai/src python3 -m teleharness team \
  --provider mock \
  --actor mobile --area frontend \
  --module M04 \
  --task "Implementar reserva de citas sin conflictos" \
  --workspace .
```

Resultados en `.ai/runs/<run-id>/`:

```text
result.json
01_orchestrator/{result.json,deliverable.md?}
02_architect/{result.json,deliverable.md?}
03_spec_creator/{result.json,deliverable.md?}
04_implementer/{result.json,deliverable.md?,proposal.patch?}
05_validator/{result.json,deliverable.md?}
proposal.patch?         # copia para revisión y aplicación explícita
```

El modelo puede completar con `artifact` vacío; un prompt de rol real pide entregables Markdown. El proveedor mock devuelve resúmenes de simulación, suficiente para probar infraestructura y handoff, no para generar código.

## Reglas de supervisión

- No se aplican cambios mientras se ejecuta `team`.
- El validador no puede aprobar legalmente una aplicación de salud ni reemplaza QA humano.
- Con un proveedor comercial, cada etapa consume llamadas API y podría tener costos según el modelo elegido.
- Para evitar contextos excesivos, cada handoff se recorta a un límite configurado; dividir historias grandes.
- Si un rol falla, no se ejecuta el siguiente; revisar su `result.json`.
- La asignación por colaborador/área viene de `.ai/collaboration.json` y se vuelve a validar antes de aplicar cambios.
