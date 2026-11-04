# Telemedicina — AI Development Harness v0.4

Harness local, **agnóstico al proveedor de IA**, para desarrollar un producto de telemedicina con **Flutter (ambos colaboradores)** y **ASP.NET Core (responsable principal: un colaborador)**. Incluye un equipo de cinco agentes, specs M01–M12, guardas de edición por área, pruebas offline y base de GitHub Actions.

> **Estado:** es un harness para desarrollo asistido, no una aplicación clínica funcional. Las carpetas Flutter/.NET son puntos de partida; todavía hay que crear esos proyectos. No se incluyen claves API ni datos de pacientes.

## 1. Equipo y estructura

| Identificador local | App Flutter | Backend ASP.NET Core | Portal web |
|---|---|---|---|
| `backend_mobile` | Sí | Responsable principal | Acceso provisional |
| `mobile` | Sí | No propone parches | Acceso provisional |

Los identificadores no autentican a nadie: son convenciones y comprobaciones locales. Los permisos reales y las revisiones se configuran en GitHub.

```text
telemedicina/
  AGENTS.md                   Reglas generales breves
  .ai/
    agents/                   Cinco agentes Markdown y team.json
    specs/                    Plan, arquitectura, backlog y M01–M12
    src/teleharness/          Motor y CLI agnósticos
    tests/                    Pruebas offline
    collaboration.json        Políticas de edición y roles
    harness.json              Configuración de proveedores
    docs/TEAM_WORKFLOW.md     Guía de GitHub y coordinación
    runs/                     Resultados locales ignorados por Git
  api/src/                    Backend ASP.NET Core (por crear)
  app/src/                    Raíz del proyecto Flutter (por crear)
  portalweb/src/              Portal médico/administrativo (por crear)
  .github/workflows/          CI harness, Flutter y .NET
```

## 2. Requisitos y comprobación sin claves

Python 3.11+, Git para aplicar parches. Flutter y .NET se instalan cuando empiecen esos proyectos. El CLI funciona sin dependencias Python externas.

En la raíz del repositorio:

```bash
PYTHONPATH=.ai/src python3 -m unittest discover -s .ai/tests -v
PYTHONPATH=.ai/src python3 -m teleharness doctor
PYTHONPATH=.ai/src python3 -m teleharness list-modules
```

Para iniciar una ejecución completa **sin credenciales**:

```bash
PYTHONPATH=.ai/src python3 -m teleharness team \
  --provider mock --actor mobile --area frontend \
  --module M04 --task "Preparar agendamiento en Flutter" \
  --workspace .
```

El proveedor `mock` ejecuta el flujo y handoffs sin generar código real, suficiente para comprobar el harness.

## 3. Uso real por cada colaborador

Cada persona configura **sus propias** variables `OPENAI_API_KEY` y/o `ANTHROPIC_API_KEY` en su computadora; nunca en Git. Las claves no se solicitan durante el modo mock. `COMPAT_API_KEY` es opcional para endpoints compatibles. El CLI no lee automáticamente `.env`.

### Colaborador backend + Flutter (OpenAI)

```bash
PYTHONPATH=.ai/src python3 -m teleharness team \
  --provider openai --model TU_MODELO_OPENAI \
  --actor backend_mobile --area backend \
  --module M04 --task "Implementar endpoints de agendamiento" \
  --workspace .
```

### Colaborador Flutter (Anthropic)

```bash
PYTHONPATH=.ai/src python3 -m teleharness team \
  --provider anthropic --model TU_MODELO_ANTHROPIC \
  --actor mobile --area frontend \
  --module M04 --task "Implementar pantallas de reserva" \
  --workspace .
```

### Endpoint compatible con OpenAI (incluidos locales)

```bash
PYTHONPATH=.ai/src python3 -m teleharness run --role implementer \
  --provider compatible --model TU_MODELO --base-url http://127.0.0.1:11434/v1 \
  --actor mobile --area frontend --module M04 \
  --task "Crear widget de agenda" --workspace .
```

Los endpoints remotos compatibles requieren HTTPS; HTTP solo se admite en loopback. Modelos se eligen según la disponibilidad de cada cuenta. Proveedores reales **no se probaron en vivo** porque este paquete se entregó sin credenciales.

El área `portal` permite proponer cambios únicamente en `portalweb/src/`. Está habilitada provisionalmente para ambos colaboradores hasta que decidan su responsable; la tecnología del portal sigue por definir.

## 4. Equipo de agentes

`team` realiza un flujo **secuencial** de cinco roles:

1. `ORCHESTRATOR.md`: prioridad, alcance, dependencias y área/responsable.
2. `ARCHITECT.md`: componentes, seguridad, contratos API y datos.
3. `SPEC_CREATOR.md`: historias, validaciones y criterios comprobables.
4. `IMPLEMENTER.md`: lectura del código y propuesta de un parche.
5. `VALIDATOR.md`: revisión del parche, criterios, riesgos y pruebas sugeridas.

Los handoffs y resultados quedan bajo `.ai/runs/<id>/`; no se comparten automáticamente entre equipos. Nunca se aplican parches al ejecutar `team`.

## 5. Revisar, aplicar y probar

```bash
# Ver el parche generado por un run
PYTHONPATH=.ai/src python3 -m teleharness review --run-id ID

# Aplicar con confirmación interactiva, después de inspección
PYTHONPATH=.ai/src python3 -m teleharness apply --run-id ID \
  --actor mobile --area frontend --workspace .

# Verificaciones manuales predefinidas
PYTHONPATH=.ai/src python3 -m teleharness check --preset python-tests --workspace .
PYTHONPATH=.ai/src python3 -m teleharness check --preset flutter-analyze --workspace .
PYTHONPATH=.ai/src python3 -m teleharness check --preset flutter-test --workspace .
PYTHONPATH=.ai/src python3 -m teleharness check --preset dotnet-test --workspace .
```

Los checks de Flutter/.NET requieren que los proyectos existan. El parámetro `--yes` en `apply` permite ejecución no interactiva **solo cuando el usuario haya revisado y aprobado el parche**.

## 6. GitHub y reglas de equipo

1. Crear un único repositorio privado, invitar a ambos colaboradores y trabajar en ramas por historia.
2. Conservar en Git `.ai/agents/`, `.ai/specs/`, `.ai/collaboration.json`, harness y código de app/API.
3. Excluir claves, `.env`, binarios de build, datos médicos y `.ai/runs/` (hay `.gitignore`).
4. Configurar protección de `main`, PR revisada por el otro colaborador y checks de CI.
5. Sustituir los placeholders de `.github/CODEOWNERS.example` por los usuarios de GitHub y renombrarlo cuando estén definidos.
6. Seguir [.ai/docs/TEAM_WORKFLOW.md](.ai/docs/TEAM_WORKFLOW.md) para nombrado de ramas, contrato OpenAPI y responsabilidad.

## 7. Seguridad y límites

- El modelo solo puede leer/listar/buscar texto dentro del workspace y **proponer** un diff git.
- Los parches frontend solo pueden editar `app/src/`, backend solo `api/src/` y portal solo `portalweb/src/`. `fullstack` requiere `backend_mobile`.
- Bloquea rutas sensibles, symlinks, escapes fuera del workspace, borrados/renombres, cambios binarios y comandos arbitrarios.
- `apply` comprueba de nuevo área y actor, valida el parche y solicita confirmación.
- No hay diagnósticos automáticos; utilizar datos sintéticos, cumplir políticas clínicas/legales y exigir QA humano antes de piloto.
- Los modelos pueden generar código incorrecto. El validador emite **recomendaciones**, no aprobaciones de despliegue.
