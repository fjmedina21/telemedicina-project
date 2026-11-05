# `.ai/` — Harness de ingeniería

Todo lo relativo a IA y desarrollo asistido vive aquí:

- `agents/`: [flujo y aprobación humana](agents/AGENT.md), roles individuales, configuración y guía del equipo.
- `specs/`: plan, arquitectura y las 12 especificaciones de telemedicina.
- `src/teleharness/`: CLI y adaptadores OpenAI, Anthropic, compatibles y mock.
- `collaboration.json`: participantes y rutas editables.
- `harness.json`: parámetros de ejecución y proveedores.
- `tests/`: pruebas unitarias offline.
- `docs/`: acuerdo de trabajo para dos colaboradores.
- `runs/`: resultados generados localmente, ignorados por Git.

Áreas editables: `app/src/` (Flutter), `api/src/` (backend) y `portalweb/src/` (portal médico/administrativo).

**Ejecución desde la raíz `telemedicina/`:**

```bash
PYTHONPATH=.ai/src python3 -m teleharness doctor
PYTHONPATH=.ai/src python3 -m teleharness run --role architect --provider mock --actor backend_mobile --area fullstack --module M01 --task "Preparar una propuesta técnica para M01" --workspace .
```

El comando `team` aún ejecuta el pipeline automático legado sin pausa de aprobación humana. Sigue [el flujo de agentes](agents/AGENT.md) y no uses `team` para trabajo que requiera esa aprobación. Consulta el `README.md` raíz para configuración general. No almacenar claves ni datos clínicos reales aquí.
