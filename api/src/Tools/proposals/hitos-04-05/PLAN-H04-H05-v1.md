# Plan coordinado — Hitos 04 y 05

**Versión:** H04-H05-PLAN-v1.  
**Fecha:** 2026-11-05, America/Santo_Domingo.  
**Estado:** diseños y especificaciones preparados y revisados; pendiente aprobación humana de H04-SPEC-v1 y H05-SPEC-v1. No aprobado para implementación.  
**Solicitud:** «continua con los hitos 4 y 5».  
**Ejecución:** agentes directos, sin harness, conservando la preferencia indicada por el usuario.

## Objetivo y secuencia

Completar un incremento P0 revisable sobre las funciones ya existentes, primero H04 (catálogo y operación de slots explícitos), después H05 (solicitudes/cambios de citas y revisión manual de cobertura). Preservar los cambios y evidencias del H03 completado localmente.

1. Orquestador: revisar specs base, código y dependencias; asignar responsabilidades.
2. Architect: diseños [H04-DESIGN-v1](H04-DESIGN-v1.md) y [H05-DESIGN-v1](H05-DESIGN-v1.md), preparados.
3. Spec Creator: [H04-SPEC-v1](H04-SPEC-v1.md) y [H05-SPEC-v1](H05-SPEC-v1.md) preparados, con contratos, autorización, fallos y criterios verificables.
4. Humano: revisar y aprobar explícitamente las versiones concretas y su alcance; conservar la constancia y huellas antes de Implementer.
5. Implementer: tras aprobación, proponer parche H04, revisar OpenAPI y coordinar clientes; Validator revisa; aplicar solo bajo autorización y verificar resultados reales. H05 sigue a H04 validado.
6. Cierre: evidencia persistida, revisión Validator y aprobación humana de cierre; no marcar completado de antemano.

## Alcance propuesto para aprobación

| Hito | Incluido | Alcance posterior |
|---|---|---|
| H04 | Filtros de catálogo/disponibilidad, selección profesional-servicio válida, listado administrativo sin datos de pacientes, ver/bloquear/desbloquear slots, oferta publicable y concurrencia/versionado compatible | Recurrencia, calendarios festivos automáticos, cupos múltiples, tarifas y nuevos tipos de visita |
| H05 | Perfil completo para nuevas solicitudes, replay idempotente conservado, reprogramación del mismo servicio, detalle/historial y revisión manual con fuente/vigencia y estados claros | Nuevos umbrales de anticipación/cancelación, no-show, particular/pagos, ARS automática, agenda médica y alta multicanal por personal |

Las exclusiones son decisiones propuestas de alcance para la revisión humana. El incremento P0 no equivale al cumplimiento integral de todas las funciones M03–M05. Se conserva la política operativa actual antes del inicio de la cita; no se inventan políticas clínicas ni económicas.

## Responsables y áreas

- Coordinación: orquestador; mantiene los handoffs y no aprueba en nombre del usuario.
- Backend: backend_mobile, único responsable, api/src/.
- Flutter: backend_mobile y mobile coordinados, app/src/.
- Portal: backend_mobile en área portal, portalweb/src/; sin ampliar permisos de personal.
- Orden de contratos: API/OpenAPI revisado antes de integrar Flutter/portal; un parche y evidencia diferenciados por hito.

## Criterios transversales y evidencia prevista

Mantener Patient y Admission con la matriz vigente; Administrator, Practitioner y Support no heredan funciones de citas o catálogo. Autorización en servidor, revalidación de sesión, protección de identidad, auditoría mínima, timestamps UTC y visualización America/Santo_Domingo.

Validación futura: compilación/contratos .NET, PostgreSQL con fixtures propios (migración idempotente, carreras, rollback y eventos), Flutter unit/widgets y portal Playwright, HTTP/OpenAPI y regresión H03/citas. Correo development y datos sintéticos. No se han ejecutado nuevas pruebas de H04/H05 ni se ha modificado código de aplicación en esta etapa.

## Puerta de aprobación

Aplicar .ai/agents/AGENT.md: revisión humana de las specs concretas antes de Implementer. La aprobación de H03 no constituye aprobación de H04/H05. Decisiones o versiones posteriores modificadas vuelven a revisión. PR, UAT y go/no-go de producción son etapas separadas.
