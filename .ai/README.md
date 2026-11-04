# `.ai/` — Harness de ingeniería

Todo lo relativo a IA y desarrollo asistido vive aquí:

- `agents/`: orquestador, arquitecto, spec creator, implementador y validador.
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
PYTHONPATH=.ai/src python3 -m teleharness team --provider mock --actor mobile --area frontend --module M04 --task "Probar el equipo" --workspace .
```

Consulta el `README.md` raíz para configuración y comandos completos. No almacenar claves ni datos clínicos reales aquí.
