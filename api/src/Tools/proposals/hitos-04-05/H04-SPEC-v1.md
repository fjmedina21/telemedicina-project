# Spec ejecutable — H04: catálogo y agenda manual operativa

**Versión:** H04-SPEC-v1.  
**Fecha:** 2026-11-05, America/Santo_Domingo.  
**Estado:** propuesta pendiente de revisión humana; no autoriza implementación.  
**Entrada:** [plan coordinado](PLAN-H04-H05-v1.md), [diseño H04](H04-DESIGN-v1.md), M03 y arquitectura transversal.  
**Dependencias:** matriz vigente H02 y base actual; conservar H03 completado y sus cambios locales.

## Alcance e historias

Incremento P0 sobre catálogo y slots explícitos existentes. No es la implementación integral de M03: se posponen recurrencias, calendarios de feriados automáticos, cupos múltiples, tarifas, tipos de visita y cambios configurables de zona. Pausas/feriados se representan manualmente omitiendo o bloqueando slots. La referencia de habilitación es administrativa; su presencia no constituye validación clínica o legal.

| ID | Área | Resultado y trazabilidad |
|---|---|---|
| H04-API-01 | API | Oferta coherente en catálogo, disponibilidad y escrituras; M03 regla de publicación y aceptación 1/3 |
| H04-API-02 | API | Listado de slots administrativos paginado, sin datos de pacientes; US-11 parcial |
| H04-API-03 | API | Crear, bloquear/desbloquear sin solapamientos y con versión compatible; US-11 parcial |
| H04-APP-01 | Flutter | Buscar por especialidad, servicio, profesional asociado y rango; US-09 parcial y US-10 |
| H04-WEB-01 | Portal | Seleccionar pares asociados, ver slots bloqueados/reservados, bloquear y desbloquear |
| H04-QA-01 | Transversal | Roles, UTC, carreras, migración y regresión reales; M03 aceptación 1–4 |

No se crea un rol coordinador ni se amplían permisos de Practitioner. Las operaciones administrativas siguen correspondiendo a Admission.

## Base y contratos preservados

Ya existen CRUD de catálogo con versión, asociaciones, slots, bloqueo, disponibilidad y exclusiones PostgreSQL. Mantener sus IDs, rutas, arrays y envelope success/status/detail/data. ErrorApiResponse conserva errors:string[], code y traceId, sin valores enviados ni errores SQL. No modificar globalmente errores de otros módulos.

Conservar límites de CatalogServiceRequest: name obligatorio máximo120, specialty obligatorio máximo100, durationMinutes entero5..240, approvalReference opcional máximo250, version entero>=0; Active e IsSynthetic existentes. Conservar CatalogPractitionerRequest: name obligatorio máximo120, licenseRef opcional máximo100, serviceIds obligatorio máximo100 UUID válidos, version>=0. Recortar textos antes de guardar, sin alterar las reglas de binding vigentes. Duración de un servicio con cualquier slot futuro no cambia: 409 EXISTING_SCHEDULE. Las citas existentes no se cancelan al desactivar oferta.

## Política común de oferta

Un servicio es publicable si active y, además, is_synthetic=true o approval_reference recortada no vacía. Para un slot se requiere también profesional activo y asociación activa para ese servicio. No rellenar referencias históricas ni borrar registros: la restricción NOT VALID de migración003 no garantiza que los activos históricos estén habilitados.

Aplicar la misma política a especialidades, servicios, filtro de profesionales por servicio, disponibilidad, creación de slots, desbloqueo y validación final de nuevas reservas/reprogramaciones existentes. Ajustar estas dos escrituras en H04 solo para coherencia de oferta y orden de locks; los requisitos nuevos de citas/completitud pertenecen a H05.

GET /practitioners sin filtro conserva la lista de profesionales activos y sus campos actuales; esa lista no habilita reserva. Con serviceId, exige asociación y servicio publicable. Citas previas siguen visibles aunque su oferta se desactive.

## Datos, entradas y límites

Migración007, siguiente a006 en la base inspeccionada: añadir slots.version integer NOT NULL DEFAULT1 de forma aditiva e idempotente. No modificar IDs, fechas, bloqueos ni filas de citas; no reescribir auditoría. Si aparece otra migración007 antes de implementar, resolver la numeración coordinadamente antes de integrar.

- UUID de query/cuerpo obligatorios donde se indique: presentes, parseables y distintos de Guid.Empty. Filtros opcionales omitidos significan todos; cadena vacía o UUID mal formado produce400 VALIDATION_FAILED.
- Fechas de nuevas entradas y filtros: ISO8601 con Z u offset explícito; rechazar hora sin offset y convertir a UTC. Fecha mal formada:400 VALIDATION_FAILED.
- Para consultas de horarios, from efectivo=valor suministrado o instante UTC del servidor capturado una vez. to efectivo=valor suministrado o from efectivo+14 días. Debe cumplirse 0 < to-from <=31 días; de lo contrario400 INVALID_RANGE. Se admiten rangos históricos en el listado administrativo.
- POST slot: serviceId y practitionerId UUID; startAt obligatorio y posterior al instante de validación del servidor. EndAt se calcula con la duración vigente, no se recibe del cliente. Pasado/igual a ahora:400 INVALID_DATE.
- PATCH block: blocked booleano requerido; version opcional null/omitida conserva legado; si se suministra valor, entero>=1. Campo ausente blocked o tipo/rango incorrecto:400 VALIDATION_FAILED.
- offset entero>=0; limit entero1..300, por defecto100. blocked y reserved filtros booleanos opcionales. Valores inválidos:400 VALIDATION_FAILED. specialty conserva filtro textual exacto existente; la UI usa los nombres recibidos.

## Contratos API

| Ruta | Autorización y respuesta |
|---|---|
| GET /api/v1/specialties | Patient/Admission; data array {name}, solo servicios publicables; orden name |
| GET /api/v1/services?specialty=texto | Patient/Admission; array existente {id,name,specialty,durationMinutes}; solo publicables; orden name,id |
| GET /api/v1/practitioners?serviceId=UUID | Patient/Admission; array {id,name}; filtro opcional; orden name,id |
| GET /api/v1/availability | Patient/Admission; serviceId requerido, practitionerId/from/to opcionales; array actual, sin paginar |
| GET /api/v1/admin/slots | Nuevo, Admission; from/to, serviceId/practitionerId, blocked/reserved, offset/limit opcionales; data paginada |
| POST /api/v1/admin/slots | Admission; cuerpo existente {serviceId,practitionerId,startAt}; 201 data {id,version:1} |
| PATCH /api/v1/admin/slots/{id}/block | Admission; {blocked,version?}; 200 data {blocked,version} |

Disponibilidad conserva campos id,serviceId,practitionerId,practitionerName,startAt,endAt,serviceName,specialty. Solo futuros, no bloqueados, no ocupados por PendingValidation/Confirmed, con oferta válida. Filtrar startAt>=from y endAt<=to como en el contrato actual, ordenar startAt,practitionerName,id y limitar300. No convertir a objeto paginado. Si un UUID de filtro válido no tiene coincidencias, devolver200 array vacío. La UI advierte que muestra hasta300 y permite reducir el rango; no afirma mostrar todos.

Listado administrativo: data={items,totalCount,offset,limit,hasMore}. Aplicar todos los filtros antes de contar y paginar, ordenar startAt,id, incluir slots cuyo startAt>=from y startAt<to. items y totalCount proceden de una misma sentencia SQL/snapshot, no de dos lecturas susceptibles de discrepar. hasMore=(offset+items.length)<totalCount; offset fuera del total devuelve items=[] y el total real.

Cada item contiene exactamente id,serviceId,serviceName,practitionerId,practitionerName,startAt,endAt,blocked,reserved,version,serviceActive,practitionerActive,assignmentActive,servicePublishable. reserved es EXISTS de cita PendingValidation/Confirmed en ese slot. Se muestran también ofertas actualmente desactivadas para gestión administrativa. No patientId, email, nombre de paciente, appointmentId, afiliación ni evidencia.

## Escrituras, errores y concurrencia

Creación: recurso inexistente o servicio/profesional inactivo conserva404 NOT_FOUND; activo pero sin requisito de publicación devuelve409 SLOT_UNAVAILABLE; asociación ausente/inactiva400 PRACTITIONER_SERVICE_REQUIRED; solapamiento PostgreSQL409 SCHEDULE_OVERLAP. No mover slots ni cambiar sus duraciones. POST no incorpora idempotencia nueva: tras respuesta de red incierta, la UI consulta antes de repetir; la exclusión evita un segundo slot solapado.

Bloqueo: resolver slot404; validar versión antes de cualquier no-op. Version suministrada obsoleta da409 VERSION_CONFLICT aunque el booleano coincida. Bloquear un slot reservado da409 SLOT_RESERVED. Si una transición efectiva afecta un slot reservado, también409 SLOT_RESERVED; un no-op blocked=false sobre slot ya libre de bloqueo no altera nada. Desbloqueo efectivo requiere además inicio futuro, oferta publicable y asociación activa; pasado400 INVALID_DATE, oferta o asociación no disponible409 SLOT_UNAVAILABLE.

Si el estado solicitado ya coincide y se cumplen las comprobaciones aplicables,200 con versión actual, sin incremento ni nueva auditoría. Cambio real incrementa version una vez y registra schedule.block_changed dentro de la misma transacción. Clientes nuevos siempre envían version. Sin versión, el cliente legado obtiene asignación absoluta serializada con todas las invariantes, pero no se promete compare-and-swap universal.

Orden común de recursos para H04 y H05, bajo READ COMMITTED:

1. AccountGuard: bloquear la fila users del actor (o actores ordenados por ID cuando haya más de uno) y después revalidar sesión/permisos con Require, como actualmente; no se exige un nuevo lock explícito sobre la fila de sesión. Conservar el orden de las operaciones de identidad.
2. Advisory lock de idempotencia únicamente si la operación ya lo usa; nunca añadirlo después de recursos de oferta.
3. Cita propia si la operación la modifica; SELECT FOR UPDATE y validación de versión. No bloquear citas ajenas.
4. Servicios necesarios por ID ordinal: compartido para consultar oferta, exclusivo para editar el servicio.
5. Profesionales necesarios por ID ordinal: compartido para oferta, exclusivo para editar profesional/asociaciones.
6. Releer asociación y publicación en una sentencia posterior a obtener los locks anteriores; no usar un EXISTS evaluado antes de esperar el lock como decisión final.
7. Slots necesarios por ID ordinal, FOR UPDATE; entonces releer bloqueos y ocupación con EXISTS sin bloquear la cita que ocupa el destino.
8. Mutación, historial/evento/outbox donde corresponda y auditoría; preservar el orden existente del lock delivery y no adquirir catálogo/slots después de ese punto.

Los escritores de asociaciones leen sin locks la versión y asociaciones actuales, recopilan servicios antiguos+nuevos, bloquean servicios ordenados y después profesional exclusivo, revalidan versión y conjunto previo; si cambió,409 VERSION_CONFLICT y rollback, sin adquirir recursos anteriores fuera de orden. Creación de profesional valida servicios solicitados bajo el mismo orden. Los slots conservan identidad de servicio/profesional inmutable. Una lectura preliminar solo descubre IDs; toda decisión de autorización, oferta o versión se relee bajo sus locks. Si cambió la cita al esperar su lock, abortar por conflicto, sin continuar con IDs obsoletos.

El bloqueo de horario consulta ocupación después del lock del slot sin FOR UPDATE de citas. Reprogramación comprueba destino ocupado antes de UPDATE y no espera locks de citas ajenas; dos pacientes intentando intercambiar slots ocupados reciben409, sin ciclo de espera ni500. Mantener exclusiones PostgreSQL y mapear sus conflictos a los errores de dominio existentes, con rollback íntegro y sin reintentar mutaciones automáticamente.

Linealización: si desactivación obtiene primero el recurso, la nueva oferta/reserva espera y se rechaza al releer. Si reserva confirma primero, la desactivación posterior conserva esa cita; no se interpreta como una reserva admitida después de la desactivación. No hay bloqueo global del catálogo.

## Flujos UX y fallos

Portal: servicio elegido filtra profesionales activos con serviceIds asociados; al cambiar servicio o desaparecer relación, invalidar selección/resultados y no enviar un par imposible. Tabla de slots con fechas, reservado/bloqueado y filtros/rango/paginación; mostrar bloqueados para permitir desbloqueo. Ofrecer acciones solo sobre futuros y no reservados. Nuevo portal envía versión;409 conserva formulario e invita a recargar, nunca sobreescribe automáticamente.

Flutter: repositorio/DTO en feature catalog; reutilizar ChangeNotifier, shell y cliente existentes. Secuencia especialidad→servicio→profesional opcional→rango→consulta; mostrar duración, nombre y fecha America/Santo_Domingo. Distinguir no consultado, carga, vacío, error y resultados; informar límites de atención remota y que disponibilidad no garantiza reserva. No inventar precios o recomendaciones clínicas.

Ambos clientes deshabilitan duplicados al procesar, conservan formularios ante400/red/409, ofrecen reintento explícito y limpian datos en logout/expiración detectada. Un identificador de consulta/sesión impide instalar respuestas obsoletas después de cambiar filtros o sesión. Etiquetas accesibles, foco y texto escalable; sin rediseño global ni correcciones de estilo ajenas al incremento.

## Matriz de autorización

| Actor | Catálogo/disponibilidad | Listar agenda administrativa | Cambiar catálogo/slots |
|---|---|---|---|
| Patient activo | Sí | No,403 | No,403 |
| Admission activo | Sí | Sí | Sí, permisos vigentes |
| Administrator, Practitioner, Support u otro | No,403 | No,403 | No,403 |
| Sin sesión, revocada/expirada o cuenta inválida | 401 | 401 | 401 |

La UI no sustituye estas comprobaciones. No conceder permisos médicos ni acceso a datos de pacientes.

## Aceptación Given/When/Then

1. Dado un servicio histórico activo no sintético sin referencia, al consultar catálogo/disponibilidad o intentar nueva oferta, no se publica ni reserva; no se inventa referencia ni se modifica una cita previa.
2. Dados servicio/profesional activos sin asociación, al filtrar profesionales no aparece el par y al crear slot se recibe400; si se desactiva la relación concurrentemente, se cumple la linealización descrita.
3. Dados dos POST de slots solapados del mismo profesional, cuando compiten, solo uno crea y el otro409; no hay auditoría parcial.
4. Dado slot bloqueado, al consultar disponibilidad no aparece ni puede reservarse; en admin/slots sí aparece sin datos de paciente y puede desbloquearse si cumple invariantes.
5. Dada versión obsoleta, al bloquear/desbloquear se recibe409 sin cambios; dada versión vigente y no-op, se conserva versión y número de eventos.
6. Dado cliente legado sin versión, al asignar estado se serializa sin duplicación; no puede bloquear reservado ni reactivar oferta suspendida.
7. Dado rango no positivo o >31 días, al consultar devuelve400; una fecha sin offset o UUID inválido devuelve400 seguro.
8. Dada paginación y filtros, al listar se mantiene orden determinista, total coherente e items=[] al exceder total; no se expone identidad de pacientes.
9. Dada migración007 aplicada dos veces, las filas anteriores conservan IDs/horas/bloqueos, version=1 y no hay pérdida de datos.
10. Dado cambio de filtro/sesión mientras una respuesta está en curso, el cliente conserva solo resultados de la consulta/sesión actual.
11. Dados dispositivo en otra zona y fechas equivalentes con offsets distintos, API persiste UTC y ambos clientes muestran America/Santo_Domingo.
12. Dados roles de la matriz y llamadas HTTP directas, se cumple401/403; se preservan contratos anteriores y la regresión de H03/citas.

## Verificación prevista y handoff

Pruebas futuras: unidad de filtros/política, HTTP/OpenAPI, PostgreSQL (migración doble, locks, carreras, exclusiones y rollback al fallar auditoría), widgets Flutter y Playwright de catálogo/agenda. Datos sintéticos exclusivos, correo development y sin secretos/cuerpos sensibles en evidencias. No se han ejecutado pruebas H04 ni modificado código durante esta etapa.

Comandos de verificación previstos, tras aplicar el parche bajo autorización:

| Componente | Verificación real prevista |
|---|---|
| .NET | dotnet build api/src/Telemedicina.sln --configuration Release |
| Contratos .NET de consola | dotnet run --no-build --configuration Release --project api/src/Telemedicina.Tests/Telemedicina.Tests.csproj |
| Flutter, desde app/src | flutter analyze --no-fatal-infos y flutter test |
| Portal, desde portalweb/src | npm run build y npm run test:e2e, scripts existentes comprobados en package.json |
| PostgreSQL/HTTP | Runner sintético del hito, API loopback temporal y conexión privada en memoria; comprobar resultados y códigos de salida, sin confundir tests simulados con integración real |

dotnet test no sustituye el ejecutable de contratos actual. Las pruebas del incremento deben incluir casos de la matriz anterior; compilación sola no demuestra autorización ni concurrencia.

Backend único responsable en api/src; Flutter coordinado por ambos colaboradores en app/src; portal en portalweb/src. OpenAPI y contratos revisados antes de clientes. Preparar un parche H04 separado y entregar a Validator con evidencia real. H05 depende de este contrato validado.

## Decisiones que requiere aprobar el humano

Aprobar este incremento acotado de slots manuales, publicación conservadora de históricos y transición de versión opcional para legado. Recurrencias, feriados, cupos, tarifas y tipos de visita siguen como alcance posterior explícito. Este cierre P0 no podrá denominarse M03 completo.

La etapa termina aquí: revisión y aprobación explícita de H04-SPEC-v1, registrada con huella y fecha, antes de invocar Implementer. No existe todavía esa aprobación.
