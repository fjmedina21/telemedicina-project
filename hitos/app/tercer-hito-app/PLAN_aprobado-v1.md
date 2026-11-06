# Hito 03 — Perfil del paciente (Flutter)

**Módulo:** M02 — Perfil de paciente y dependientes
**Prioridad:** P0, perfil esencial
**Versión:** H03-PLAN-v1
**Estado:** Plan aprobado; especificación ejecutable pendiente
**Área autorizada:** `app/src/`

## Objetivo
Permitir consultar y actualizar perfil, contacto de emergencia y declaraciones de salud desde Flutter, distinguiéndolas de diagnósticos confirmados.

## Alcance P0
- Mi perfil, datos esenciales y estado de completitud.
- Edición de contacto permitido y contacto de emergencia.
- Captura y edición de alergias, medicamentos y antecedentes autodeclarados.
- Estados de carga, guardado, éxito, error, sesión vencida y perfil incompleto.
- Validación de formato en cliente y validación final en servidor.
- Fuente y fecha según contrato; identificadores verificados no editables ordinariamente e información sobre corrección revisable.
- Integración con repositorio, API y almacenamiento seguro existentes.

## Fuera de alcance
Dependientes, decisiones clínicas, campos fuera del contrato, persistencia local de expedientes o declaraciones completas y seguro (M05).

## UX y privacidad
Solicitar campos necesarios aprobados; identificar información autodeclarada; evitar datos sensibles en notificaciones, telemetría, errores y depuración. Incluir etiquetas comprensibles, foco, texto escalable y errores asociados al campo.

## Criterios de aceptación y validación prevista
1. Consultar y editar campos permitidos confirma el guardado del servidor.
2. Errores de validación aparecen asociados a campos cuando sea posible.
3. Fallos de red y sesión vencida ofrecen reintento o autenticación.
4. Alergias, medicamentos y antecedentes se identifican como autodeclarados.
5. Identificadores verificados no se editan ordinariamente.
6. Perfil incompleto identifica pendientes necesarios para el siguiente flujo.
7. No persiste información clínica fuera de las necesidades aprobadas de sesión.

Validación: widgets para carga, edición, guardado y errores; accesibilidad, foco y texto escalable; contrato y ausencia de logs sensibles; flujo con datos sintéticos. Estas verificaciones están planificadas, no ejecutadas.

## Dependencias y decisiones pendientes
OpenAPI aprobado; campos obligatorios y presentación; categorías y límites; flujo informativo de corrección de identidad. Reutilizar estado, navegación, tema y cliente HTTP existentes.

## Handoff
Integración tras aprobación del contrato y especificación ejecutable. Cambios de alcance requieren nueva revisión humana.
