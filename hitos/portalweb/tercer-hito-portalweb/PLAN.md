# Hito 03 — Perfil del paciente (Portal Web)

**Módulo:** M02 — Perfil de paciente y dependientes
**Prioridad:** P0, soporte administrativo mínimo
**Versión:** H03-PLAN-v1
**Estado:** Plan aprobado; alcance administrativo condicionado
**Área autorizada:** `portalweb/src/`

## Objetivo
Ofrecer vista administrativa mínima para tareas autorizadas sobre perfil y contacto, con trazabilidad de los cambios permitidos.

## Alcance condicionado
La spec M02 no detalla suficientemente permisos y pantallas administrativas. Aprobar antes de implementar:
- Consulta de datos mínimos necesarios para la tarea autorizada.
- Correcciones administrativas solo con permiso explícito y procedimiento revisable.
- Fuente y fecha de declaraciones, identificadas como autodeclaradas.
- Avisos de perfil incompleto sin exponer campos innecesarios al rol.
- Estado de solicitud de corrección de identidad, si se define ese proceso.
- Consumo de endpoints y DTO aprobados.

## Fuera de alcance
Edición libre, fusión de expedientes, dependientes, acceso general por cuenta administrativa, validación clínica automática, exportación masiva y desarrollo previo a aprobar roles, permisos y contrato.

## Seguridad
Mínimo privilegio por rol y tarea; autorización en servidor por lectura y escritura; auditoría según contrato; sin datos personales o sanitarios en logs, URLs, telemetría o errores. Solo datos sintéticos para desarrollo y validación.

## Criterios de aceptación y validación prevista
1. Roles sin permiso no acceden ni mediante llamadas directas.
2. Personal autorizado ve solo campos necesarios para su tarea.
3. Se distinguen declaraciones e información validada profesionalmente.
4. Cambios administrativos permitidos muestran resultado y auditoría verificable.
5. No se fusionan registros ni cambian identificadores verificados sin procedimiento aprobado.
6. Errores no revelan existencia de pacientes ajenos a actores no autorizados.

Validación: permisos por rol y endpoints, acceso fuera de tarea, ausencia de datos sensibles en errores/logs/URLs, carga y sesión vencida, accesibilidad y datos sintéticos. Estas verificaciones están planificadas, no ejecutadas.

## Decisiones pendientes
Necesidad de pantalla de portal; tareas y campos visibles para admisión, médico y soporte; corrección de identidad; permisos de corrección de contacto; contrato backend.

## Handoff
El desarrollo del portal requiere decisión explícita sobre alcance y permisos y aprobación de la especificación ejecutable. Este plan no autoriza implementación.
