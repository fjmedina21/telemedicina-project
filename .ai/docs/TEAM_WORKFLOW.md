# Flujo de trabajo — dos colaboradores

## Responsabilidades

| Identificador del harness | Backend | Flutter | Portal web | Revisión |
|---|---|---|---|---|
| `backend_mobile` | Responsable principal | Compartido | Provisional | PR del otro colaborador |
| `mobile` | No modifica por defecto | Compartido | Provisional | PR del otro colaborador |

Los identificadores son **convenciones locales autodeclaradas**, no un sistema de autenticación. GitHub administra el acceso real al repositorio. No compartir contraseñas o claves API.

## Estructura Git

```text
app/src/           Raíz Flutter, ambos colaboradores
api/src/           ASP.NET Core, backend_mobile
portalweb/src/     Portal médico/administrativo (tecnología por definir)
.ai/agents/        Instrucciones de los cinco agentes
.ai/specs/         Especificaciones originales y decisiones aprobadas
.ai/src/teleharness/ Motor local, común para ambos
.ai/docs/          Contratos, ADR y acuerdos
.github/workflows/ CI común
```

## Convenciones de ramas

- `feature/m01-auth-mobile`, `feature/m04-booking-mobile` para Flutter.
- `feature/m04-appointments-api` para backend.
- `fix/m04-slot-conflict-api` para correcciones.
- `docs/m04-contract` para documentos de contratos.
- `feature/m04-clinician-portal` para el portal web.

**Una historia y una rama por PR.** No reservar simultáneamente la misma historia o archivos transversales sin coordinar. Archivos especialmente compartidos: `pubspec.yaml`, router, tema, cliente HTTP y modelos de sesión.

## Contrato API-first

1. El backend publica OpenAPI como contrato (versionado junto al código).
2. Ambos desarrolladores Flutter trabajan con modelos, DTOs y mocks basados en ese contrato.
3. Cambios incompatibles se anuncian y se ajustan en la misma PR o con una transición versionada.
4. Se prueban estados de error, autorización, concurrencia y reintentos; la UI no debe simular coberturas o pagos aprobados.

El portal dispone del área `portal`, provisionalmente editable por ambos. Definir tecnología y responsable antes de desarrollar sus historias. Compartirá los contratos versionados de la API.

## Pull Request mínimo

- [ ] Historia/spec y alcance.
- [ ] Capturas o grabación de UI sin datos sensibles, si aplica.
- [ ] Contratos API/DTO actualizados, si aplica.
- [ ] Pruebas locales de Flutter o .NET.
- [ ] Un colaborador diferente revisó la PR.
- [ ] Sin claves, tokens, archivos `.env`, logs de pacientes ni `.ai/runs/`.

## GitHub: configuración manual necesaria

- Activar rama protegida `main` con una revisión y checks requeridos.
- Configurar detección de secretos y push protection según capacidades de la cuenta.
- Rellenar `.github/CODEOWNERS.example` con los GitHub handles reales de ambos y renombrarlo a `.github/CODEOWNERS`.
- Registrar decisiones de arquitectura importantes como ADR en `docs/`.

## Ejemplos del harness

```bash
# Colaborador responsable de backend
PYTHONPATH=.ai/src python3 -m teleharness team --provider mock \
  --actor backend_mobile --area backend --module M04 \
  --task "Diseñar endpoints de citas" --workspace .

# Ambos pueden ejecutar frontend
PYTHONPATH=.ai/src python3 -m teleharness team --provider mock \
  --actor mobile --area frontend --module M04 \
  --task "Diseñar agenda Flutter" --workspace .
```

Las claves de OpenAI/Anthropic son específicas de cada máquina y se inyectan mediante variables de entorno. El ZIP no contiene ninguna credencial. Los artefactos `.ai/runs/` quedan locales y no se versionan; las decisiones aprobadas se trasladan a `.ai/specs`/`.ai/docs` o a la PR.
