# Diseño técnico — Hito 04: catálogo y agendas operativas

**Versión:** H04-DESIGN-v1.  
**Estado:** propuesta para Spec Creator y revisión humana; no implementada ni aprobada.  
**Módulo:** M03.  
**Dependencia:** H02 y base real actual; preservar H03 completado y sus cambios locales.

## Contexto y decisiones

La base ya ofrece CRUD Admission de servicios/profesionales con versión, asociación activa practitioner_services, creación de slots individuales, bloqueo/desbloqueo, disponibilidad futura y restricciones PostgreSQL contra solapamiento. Flutter consulta servicios y horarios; portal administra catálogo y horarios. No reconstruir estas funciones.

Brechas verificadas: SchedulePanel selecciona servicio y profesional independientemente; consulta solamente disponibilidad libre, por lo que no permite localizar/desbloquear bloqueados. GET /practitioners carece de filtro por servicio. Flutter no ofrece filtros de especialidad/profesional/rango ni distingue una consulta vacía de una consulta todavía no ejecutada. Slots y BlockRequest no tienen versión. Publicación histórica: la restricción service_publication de migración 003 es NOT VALID; pueden existir activos no sintéticos sin referencia de habilitación.

**Propuesta de alcance P0 para aprobación:** operar slots explícitos existentes, filtros y calendario por rango; ver, bloquear y desbloquear horarios no reservados; coherencia de publicación y asociaciones; control de versión para clientes nuevos y regresión transaccional. No introducir motor recurrente, feriados automáticos, reglas clínicas, cupos múltiples por profesional, tarifas ni habilitación de credenciales. Pausas/feriados solo se representan mediante omitir o bloquear slots explícitos. El cierre de este incremento no equivale a M03 completo en sus funciones futuras.

## Componentes e interacciones

- API: mantener Catalog, Scheduling y servicios SQL existentes; extraer una política común de oferta publicable reutilizable en lectura, creación de slots y reserva/reprogramación. OpenAPI antes de clientes.
- Portal Admission: reutilizar CatalogPanel y SchedulePanel. Cargar relaciones desde GET /admin/catalog/practitioners, filtrar por active y serviceIds; corregir selección inválida al cambiar servicio o al desactivarse una relación. Tabla de slots del nuevo endpoint administrativo, rango y filtro bloqueado/libre/reservado, versión y acción explícita.
- Flutter Patient: repositorio/DTO de catálogo dentro de feature, estado existente ChangeNotifier; especialidad, servicio, profesional opcional y rango; resultados vacíos claros, duración y hora de Santo Domingo, disponibilidad informativa hasta reservar. No cambiar gestor de estado ni diseño global.

## Modelo de datos

Conservar servicios, profesionales y asociaciones. Añadir slots.version integer NOT NULL DEFAULT 1 con migración aditiva idempotente; nunca cambiar IDs, fechas ni bloqueos históricos. No añadir recurrencias ni calendario festivo.

Oferta publicable: servicio activo y (is_synthetic o approval_reference recortada no vacía), profesional activo y asociación activa. Sintético solo significa demostración; no es habilitación clínica. Referencia administrativa no valida legalmente un servicio ni credenciales. Ocultar oferta no habilitada sin borrar filas ni cancelar citas existentes. No rellenar referencias históricas inventadas.

## APIs y contratos propuestos

Envelope vigente success/status/detail/data y ErrorApiResponse con errors:string[]; no cambiar errores globales. Nuevos DTO tipados y ProducesResponseType para OpenAPI.

| Ruta | Contrato |
|---|---|
| GET /api/v1/specialties | Mantener array {name}; solo especialidades con servicio publicable |
| GET /api/v1/services?specialty=... | Mantener array {id,name,specialty,durationMinutes}; filtro existente; aplicar publicación común |
| GET /api/v1/practitioners?serviceId=UUID | Query opcional nueva; con serviceId, solo profesionales activos asociados al servicio publicable; array {id,name} existente |
| GET /api/v1/availability?serviceId=UUID&practitionerId=UUID&from=ISO&to=ISO | Mantener array y campos; parámetros UUID válidos; rango positivo máximo 31 días; sin to usar from efectivo +14 días; orden startAt, practitionerName, id; máximo 300, indicar en UI el límite y permitir acotar filtros |
| GET /api/v1/admin/slots | Nuevo Admission-only: from/to, serviceId/practitionerId opcionales, blocked bool opcional, offset>=0 y limit 1..300 (100 por defecto). data:{items,totalCount,offset,limit,hasMore}; orden startAt,id. Rango máximo 31 días; por defecto hoy a +14 días |
| POST /api/v1/admin/slots | Conservar {serviceId,practitionerId,startAt}; exigir oferta publicable y futura; hora con offset/Z; 201 data:{id,version:1}, version aditiva |
| PATCH /api/v1/admin/slots/{id}/block | Conservar blocked y añadir version nullable opcional. Nuevo portal siempre envía version; 200 conserva blocked y añade version |

Item administrativo: id, serviceId, serviceName, practitionerId, practitionerName, startAt, endAt, blocked, reserved:boolean y version. No patientId, email, nombre de paciente, evidencia ni appointmentId. Puede mostrar slots de oferta ahora desactivada para explicar su estado; añadir serviceActive, practitionerActive y assignmentActive como metadatos administrativos si son necesarios. reserved significa cita PendingValidation o Confirmed vigente en ese slot, no elegibilidad clínica.

Para desbloquear, validar además oferta todavía publicable, relación activa y fecha futura; no recrear oferta suspendida. No mover ni editar la duración de slots existentes. Cambiar duración de servicio con slots futuros conserva EXISTING_SCHEDULE.

400: parámetros inválidos, INVALID_RANGE, INVALID_DATE, CATALOG_VALIDATION o PRACTITIONER_SERVICE_REQUIRED según contrato existente. 401/403 según sesión/rol; 404 recurso inexistente; 409 VERSION_CONFLICT, SCHEDULE_OVERLAP, SLOT_RESERVED, EXISTING_SCHEDULE o SLOT_UNAVAILABLE. Nunca eco de valores enviados ni excepciones SQL.

## Seguridad, privacidad y concurrencia

Roles actuales: Patient/Admission leen catálogo y disponibilidad; solo Admission modifica catálogo/agendas y lista slots administrativos. Administrator, Practitioner y Support no heredan permisos nuevos. Cuenta activa/sesión válida en servidor y AccountGuard en escrituras.

Versionado compatible de bloqueo: bajo bloqueo FOR UPDATE del slot, si version fue suministrada y difiere, 409; si coincide y estado solicitado ya es actual, éxito sin incrementar versión ni emitir nueva auditoría. Si version se omitió por un cliente legado, conservar asignación absoluta serializada y comprobar todas las invariantes. Incrementar version y auditar solo el cambio efectivo. No prometer compare-and-swap universal mientras exista cliente legado sin version. No permitir bloquear un slot reservado, con o sin version. Una solicitud obsoleta con version falla incluso si coincide el booleano final.

Conservar exclusiones PostgreSQL para slots y citas. Compartir una comprobación de oferta dentro de la transacción: bloquear filas de profesional/servicio antes de releer la asociación activa en una sentencia posterior; todo escritor de asociaciones debe bloquear el profesional. No basarse solo en EXISTS evaluado antes de esperar un lock. Evitar orden opuesto de locks entre creación/reserva y cambios de catálogo; el Spec Creator debe fijar el orden actor, profesional/servicio y slot con pruebas de carrera. No introducir una cola global ni un bloqueo global de todos los profesionales.

La desactivación de servicio/profesional/asociación impide nueva oferta y nuevas reservas; conserva citas previas y las muestra en operación para gestión humana posterior. No cancela ni reprograma automáticamente. Doble bloqueo idéntico no produce eventos duplicados. Auditoría registra acción/recurso/actor/momento sin datos del paciente.

## UX y zona horaria

Persistencia y API UTC; ISO 8601 con offset explícito o Z; portal rotula entrada America/Santo_Domingo y convierte a UTC. Flutter muestra misma zona independiente de zona del dispositivo. No afirmar soporte de cambios configurables de zona/recurrencias si solo se conserva esta zona del proyecto.

Pantallas muestran loading, empty, error, refresh, conflicto409 y sesión expirada. En conflicto conservar formulario y ofrecer recarga, sin sobrescritura automática. Campos y controles tienen etiquetas, teclado/foco y texto escalable. Deshabilitar duplicados mientras se procesa. Cambiar filtros invalida resultados anteriores; respuestas tardías no reinstalan selección ni datos de otra sesión.

## Plan de pruebas (propuesto; no ejecutado)

1. Matriz de roles por HTTP, sesión revocada y parámetros inválidos, sin PHI en slots administrativos.
2. Oferta histórica activa sin habilitación queda fuera de catálogo/disponibilidad/nuevos slots/reservas; sintéticos y habilitados válidos permanecen.
3. Servicio-profesional no asociado, asociación desactivada y cambios concurrentes contra creación/reserva; ninguna operación admite oferta inválida después del orden transaccional acordado.
4. Doble slot solapado concurrente: uno confirma, otro409; bloqueado no aparece ni se reserva.
5. Block/unblock con versión, conflicto, no-op y legado sin versión; no bloquear reservado; solo un evento por cambio.
6. Migración doble conserva IDs/fechas/estado; slots existentes reciben versión1.
7. Portal Playwright: par asociado, listado bloqueados, desbloqueo y conflicto; Flutter widgets: filtros, vacío, error, tiempo local y respuestas obsoletas.
8. Regresión H03, solicitudes de citas y contrato previo de arrays; datos sintéticos/correo development.

## Archivos previstos y handoff al Spec Creator

API: CatalogReadController.cs, CatalogController.cs cuando haga falta la política común, AvailabilityController.cs, SchedulesController.cs, SchedulingContracts.cs, nueva regla de oferta compartida y migración 007 (numeración a verificar), DTO/OpenAPI y pruebas.
Portal: PortalConsole, SchedulePanel, CatalogPanel solo errores/contratos necesarios, tipos/repositorio y Playwright existentes. Flutter: feature catalog data/presentation, PatientController/PatientShell y tests; no reestructurar otras features.

Convertir en H04-SPEC-v1 verificable con campos, límites, orden de locks y compatibilidad anterior explícitos. Presentar para aprobación: slots manuales P0, publicación histórica conservadora, versionado legado y exclusión de recurrencia/feriados/precios/tipos de atención aún no modelados. Ningún Implementer debe invocarse antes de aprobación de esa spec concreta.

## Contrato API para coordinación

Backend único responsable; Flutter coordinado por ambos colaboradores y portal en su área. Primero cerrar y revisar OpenAPI, luego clientes. H05 depende de la política común de oferta y de los contratos H04; mantener sus parches y evidencias separados. No modificar código ni ejecutar proveedores/servicios durante esta etapa de diseño.
