# Equipo de agentes — Telemedicina

El flujo de trabajo requerido está definido en [AGENT.md](AGENT.md): el orquestador siempre coordina y asigna; después se ejecutan arquitecto, creador de specs, revisión/aprobación humana, implementador y validador. La aprobación humana es una puerta obligatoria antes de implementar.

El `team.json` y el comando `teleharness team` todavía ejecutan el pipeline automático anterior y no implementan esa pausa humana. Hasta que el harness soporte la aprobación y la reanudación del mismo run, no uses `team` para una ejecución que deba respetar esta puerta; ejecuta las etapas una por una y conserva el handoff aprobado.

| Etapa | Responsable | Trabajo principal | Puede proponer patch |
|---|---|---|---|
| Coordinación permanente | `ORCHESTRATOR.md` | Asignar, invocar y coordinar handoffs según responsabilidad | No |
| 1 | `ARCHITECT.md` | Diseñar componentes, datos, APIs y seguridad | No |
| 2 | `SPEC_CREATOR.md` | Refinar historias y pruebas verificables | No |
| 3 | Humano | Revisar y aprobar explícitamente la spec antes de implementar | No |
| 4 | `IMPLEMENTER.md` | Implementar la spec aprobada y proponer un único patch | Sí |
| 5 | `VALIDATOR.md` | Revisar propuesta, riesgos y pruebas pendientes | No |

## Ejecución y handoff

El comando `teleharness run --role <rol>` ejecuta un rol individual. Para respetar el flujo aprobado, el orquestador coordina las ejecuciones por separado, conserva cada entregable e incluye su ruta en la tarea del siguiente rol. Después de `spec_creator`, se detiene para que una persona revise y registre la aprobación; solo entonces invoca `implementer`.

El comando `teleharness team` es legado: encadena los roles definidos actualmente en `team.json` sin pausa ni aprobación humana. No lo uses para este flujo hasta que el harness implemente el gate y la reanudación del mismo run.

El modelo puede completar con `artifact` vacío; un prompt de rol real pide entregables Markdown. El proveedor mock devuelve resúmenes de simulación, suficiente para probar infraestructura y handoff, no para generar código.

## Reglas de supervisión

- No se aplican cambios mientras se ejecuta `team`.
- El validador no puede aprobar legalmente una aplicación de salud ni reemplaza QA humano.
- Con un proveedor comercial, cada etapa consume llamadas API y podría tener costos según el modelo elegido.
- Para evitar contextos excesivos, cada handoff se recorta a un límite configurado; dividir historias grandes.
- Si un rol falla, no se ejecuta el siguiente; revisar su `result.json`.
- La asignación por colaborador/área viene de `.ai/collaboration.json` y se vuelve a validar antes de aplicar cambios.
- El orquestador conserva la responsabilidad de asignar e invocar cada etapa; una aprobación humana no se infiere de silencio ni de un resultado automático.
