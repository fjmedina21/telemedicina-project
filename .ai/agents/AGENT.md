# Flujo de trabajo de agentes — Telemedicina

## Flujo obligatorio

El orquestador participa siempre como coordinador: asigna el trabajo, invoca a cada agente según su responsabilidad, transmite los handoffs y controla el orden. No reemplaza a los agentes especialistas ni puede aprobar en nombre del humano.

1. **Architect:** define el diseño técnico, contratos, seguridad y decisiones abiertas.
2. **Spec Creator:** convierte el diseño y las specs base en una especificación implementable y criterios verificables.
3. **Humano:** revisa el diseño/spec, pide cambios o aprueba explícitamente. Esta es una pausa obligatoria.
4. **Implementer:** solo después de la aprobación explícita, propone un único patch conforme a la versión aprobada.
5. **Validator:** revisa el patch, criterios, riesgos y pruebas pendientes.

El orquestador asigna e invoca estas etapas en orden. Si la persona solicita cambios, el orquestador devuelve el trabajo al agente responsable y vuelve a presentar el resultado para aprobación. La aprobación se vincula a una versión concreta de la spec; una modificación posterior requiere nueva aprobación.

## Evidencia de aprobación

Antes de invocar a Implementer debe existir una constancia revisable, por ejemplo `aprobacion-humana.md`, que incluya:

- identificador de la ejecución y versión/ruta de la spec aprobada;
- decisión explícita de aprobar;
- observaciones o límites de alcance;
- fecha y responsable humano, según la política del equipo.

El silencio, el paso del tiempo o el resultado de otro agente no cuentan como aprobación. Sin constancia o si la spec cambió, el flujo se detiene antes de implementar.

## Responsabilidad del orquestador

- Selecciona módulo, área y responsables según `.ai/collaboration.json` y las instrucciones del workspace.
- Invoca Architect y Spec Creator y entrega a la persona un resumen revisable.
- Mantiene el estado en espera hasta recibir aprobación o cambios.
- Tras la aprobación, invoca Implementer y después Validator con el handoff aprobado.
- Informa bloqueos, decisiones pendientes y resultados sin afirmar pruebas no ejecutadas.
- No aplica parches ni autoriza despliegues.

## Límite actual del harness

La documentación define el flujo requerido, pero el comando `teleharness team` todavía ejecuta el orden automático antiguo y no tiene una pausa ni una reanudación con aprobación humana. No debe usarse para saltarse esta puerta. Hasta que el harness incorpore el control, el orquestador debe ejecutar las etapas por separado, guardar el handoff y reanudar solo tras registrar la aprobación.
