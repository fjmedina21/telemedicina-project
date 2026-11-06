# Hito 03 — Perfil del paciente (API)

**Módulo:** M02 — Perfil de paciente y dependientes
**Prioridad:** P0, perfil esencial
**Versión:** H03-PLAN-v1
**Estado:** Plan aprobado; especificación ejecutable pendiente
**Área autorizada:** `api/src/`

## Objetivo
Completar el perfil del paciente autenticado: contacto, contacto de emergencia y datos de salud autodeclarados, con fuente, fecha y auditoría.

## Alcance P0
- Consultar y actualizar el perfil y los datos de contacto del paciente autenticado.
- Registrar y actualizar alergias, medicamentos habituales y antecedentes relevantes como declaraciones del paciente, con fuente y fecha.
- Registrar y actualizar contacto de emergencia.
- Validar campos obligatorios y comunicar cuáles faltan para completar el perfil.
- Auditar actor, acción, recurso, resultado y momento sin duplicar contenido clínico en logs.
- Restringir lectura y escritura al paciente autenticado según permisos del servidor.
- Proteger identificadores legales verificados mediante un flujo de corrección revisable.
- Revisar modelos y contratos existentes y publicar OpenAPI antes de integrar clientes.

## Fuera de alcance
Dependientes y tutores (P1), fusión automática de expedientes, diagnósticos automáticos, integraciones de seguros (M05), cambios de roles y recopilación de datos sin propósito aprobado.

## Contratos propuestos
Rutas candidatas sujetas a revisión de lo existente:
- `GET/PATCH /api/v1/patients/me`
- `GET/PUT /api/v1/patients/me/health-declarations`
- Contacto de emergencia dentro del perfil o en recursos equivalentes, según contrato aprobado.

DTO mínimos con fuente autodeclarada y fecha. Errores `application/problem+json` sin detalles sensibles.

## Criterios de aceptación y validación prevista
1. Consultar y editar campos permitidos conserva actor y momento del cambio.
2. Manipular identificadores no permite leer ni modificar perfiles ajenos.
3. Declaraciones conservan fuente y fecha y nunca se presentan como diagnósticos confirmados.
4. Campos inválidos y cambios no permitidos producen errores seguros.
5. Actualización ordinaria no cambia identificadores legales verificados.
6. El consumidor identifica datos obligatorios faltantes sin recibir información innecesaria.
7. OpenAPI coincide con el contrato aprobado y usa ejemplos sintéticos.

Validación: autorización y acceso cruzado, validación de campos, protección del identificador, auditoría mínima y revisión del contrato con clientes. Estas verificaciones están planificadas, no ejecutadas.

## Decisiones pendientes
Campos obligatorios del piloto; categorías y límites de declaraciones; procedimiento y responsable de corrección de identidad; retención y presentación del contacto de emergencia; ampliación o versionado de contratos existentes.

## Handoff
Arquitectura y Spec Creator prepararán diseño y especificación ejecutable. La aprobación del plan no autoriza implementación: la especificación posterior requiere revisión y aprobación explícita.
