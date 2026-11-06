# Revisión conjunta — H04-SPEC-v1 y H05-SPEC-v1

**Fecha:** 2026-11-05.  
**Estado:** propuestas preparadas por Architect y Spec Creator; revisión de orquestación realizada. Aprobación humana pendiente.  
**Secuencia propuesta:** implementar y verificar H04 antes de integrar H05.

| Hito | Resultado propuesto para aprobación |
|---|---|
| [H04-SPEC-v1](H04-SPEC-v1.md) | Catálogo con publicación coherente, filtros de especialidad/profesional/rango, agenda de slots explícitos, listado de bloqueados/reservados sin datos de pacientes y bloqueo/desbloqueo con versión compatible |
| [H05-SPEC-v1](H05-SPEC-v1.md) | Perfil completo para nuevas solicitudes, idempotencia histórica preservada, reprogramación del mismo servicio, detalle/historial, conflictos y revisión manual de cobertura con vigencia clara |

## Decisiones de alcance que se someten a revisión

- H04 conserva agenda manual de slots explícitos y zona America/Santo_Domingo. Recurrencia, feriados automáticos, cupos múltiples, tarifas y tipos de visita quedan para otra especificación.
- H04 mantiene clientes legado sin versión en /block; los clientes nuevos siempre la envían. La comprobación de versión se garantiza cuando se suministra, sin prometer control de versión universal a clientes antiguos.
- H04 deja de publicar oferta histórica activa que carezca de marca sintética o referencia administrativa de habilitación, sin inventar referencias ni cancelar citas existentes.
- H05 exige completitud administrativa solo para una nueva reserva, después de resolver un replay existente. La completitud no determina aptitud clínica.
- H05 conserva la política actual de solicitud/cambio antes del inicio; no introduce nuevos umbrales, no-show, particular/pagos, ARS automática, agenda médica ni alta multicanal por personal.
- Revisión manual favorable y cita confirmada permanecen separadas de una autorización de ARS. La caducidad informativa no cancela la cita automáticamente.

Este incremento P0 no equivale al cumplimiento integral de todas las funciones M03–M05. Las specs y sus pruebas enumeran ese límite explícitamente.

## Entregables y verificación de esta etapa

[Plan coordinado](PLAN-H04-H05-v1.md), [diseño H04](H04-DESIGN-v1.md) y [diseño H05](H05-DESIGN-v1.md).

Se verificaron enlaces locales, versiones, huellas y conservación de los parches H03 aplicados. No se modificó código de aplicación ni se generaron parches de implementación H04/H05; no se ejecutaron nuevas pruebas funcionales de esos hitos. Las pruebas descritas en las specs son futuras, tras aprobación e implementación.

## Aprobación requerida

El flujo vigente .ai/agents/AGENT.md exige: «Humano: revisa el diseño/spec, pide cambios o aprueba explícitamente. Esta es una pausa obligatoria». Architect y Spec Creator ya entregaron sus resultados; Implementer debe esperar la aprobación de estas versiones concretas. No existe una constancia de aprobación H04/H05.

El usuario puede aprobar ambas specs y el alcance anterior para preparar los parches, revisarlos, autorizar su aplicación y ejecutar la validación, en orden H04 y después H05. Cualquier cambio posterior del contrato o alcance se versiona y vuelve a revisión. No se ha autorizado despliegue ni producción.

## Huellas de las versiones propuestas

- H04-SPEC-v1: `96f1f2b113d00e411d852286e8fa46802544cb81cb311a782129ab12fe6d41fa`.
- H05-SPEC-v1: `413cbf04f22add637589fbe00e51c2946a62972fa8eb9455bab58a431639b742`.
