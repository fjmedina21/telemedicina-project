# Spec ejecutable — H05: citas y revisión manual de cobertura

**Versión:** H05-SPEC-v1.  
**Fecha:** 2026-11-05, America/Santo_Domingo.  
**Estado:** propuesta pendiente de revisión humana; no autoriza implementación.  
**Entrada:** [plan coordinado](PLAN-H04-H05-v1.md), [diseño H05](H05-DESIGN-v1.md), M04/M05 y arquitectura.  
**Dependencias:** H03 completado y [H04-SPEC-v1](H04-SPEC-v1.md) implementada y validada antes de integrar H05.

## Alcance e historias

Conservar el flujo existente: Patient solicita, cancela/reprograma antes del inicio; Admission revisa manualmente con evidencia y vigencia; no hay ARS automática ni segundo sistema de cobertura.

| ID | Área | Resultado y trazabilidad |
|---|---|---|
| H05-API-01 | API | Perfil administrativamente completo en nuevas solicitudes, sin romper idempotencia; US-12 parcial |
| H05-API-02 | API | Cancelación/reprogramación del mismo servicio, versiones y eventos atómicos; US-13 |
| H05-API-03 | API | Revisión manual y proyección de vigencia coherentes, sin mutación en GET; US-17/18 |
| H05-APP-01 | Flutter | Resumen antes de solicitar, detalle/historial y gestión explícita de errores/conflictos |
| H05-WEB-01 | Portal | Cola filtrable, detalle/historial y evidencia conservada ante fallos |
| H05-QA-01 | Transversal | Contratos, aislamiento, concurrencia, rollback y deduplicación con datos sintéticos |

El alcance P0 excluye nuevos umbrales por servicio, límites de cambios, no-show, Completed/cierre clínico, precios/copagos/particular/pagos, motivo clínico libre, pólizas completas, Authorization, ARS automática, agenda Practitioner y alta multicanal por personal. US-14, US-15, US-19 y esas partes de US-12/16 quedan pendientes. No declarar M04/M05 completos por cerrar este incremento.

## Datos y validaciones

Mantener tablas y contratos existentes. No necesita migración adicional sobre H04; las proyecciones nuevas son calculadas. No reconstruir ni editar historial append-only.

BookRequest conserva exactamente slotId UUID, insurer string obligatorio máximo100 y memberLast4 string de cuatro dígitos ASCII. Mantener binding/annotations vigentes y el control INSURER_REQUIRED si insurer solo contiene espacios. Recortar insurer al almacenar como hoy; no recopilar número de afiliación completo. La identidad siempre procede de la sesión; ningún ID o estado extra del cuerpo puede cambiar sujeto, autor o estados de servidor. No introducir reglas globales nuevas para propiedades desconocidas.

Cancelación conserva VersionRequest con version entero>=1; reprogramación conserva slotId UUID y version>=1. UUID ausente/vacío o tipo inválido se rechaza400 VALIDATION_FAILED; no alterar validación de otros módulos.

CoverageRequest mantiene status exactamente Approved|Denied|ManualReview, evidence obligatoria máximo250 según binding actual, validUntil nullable y version>=1. Evidence se recorta antes de guardar; espacios solos o Approved sin vigencia válida dan400 EVIDENCE_REQUIRED. Fechas suministradas deben ser ISO8601 con offset/Z. Approved exige validUntil >= endAt y validUntil > instante UTC actual, además de que la cita no haya iniciado. Igual a endAt satisface cobertura temporal; igual a ahora no es vigente. Para Denied o ManualReview, ignorar un validUntil válido enviado y persistir null; un valor con tipo/fecha inválida sigue siendo400 de binding. No interpretar evidencia como autorización comprobada de una ARS.

## Idempotencia y completitud

Mantener el header Idempotency-Key actual: Guid.TryParse, texto literal obtenido del header y advisory lock usuario+":"+clave. No canonicalizar ni recortar la representación de claves ya aceptadas. Conservar BookRequest, orden/serialización Json del contrato y algoritmo Hash(JsonSerializer.Serialize(request, Json)) actuales. No añadir propiedades, campos nullable ni normalizar cuerpo antes de calcular esa huella. Diferencias que antes producían otra huella siguen produciéndola; no recalcular ni borrar hashes históricos.

En una solicitud autenticada y válida según binding, aplicar este orden:

1. Validar header y reglas actuales del cuerpo; Begin/AccountGuard revalida cuenta/sesión.
2. Tomar advisory lock de idempotencia; buscar por el mismo usuario y texto literal de clave.
3. Si existe, misma huella devuelve200 con la respuesta originalmente guardada, sin nueva validación de perfil/oferta, versión, auditoría, historial ni eventos; huella distinta409 IDEMPOTENCY_MISMATCH.
4. Solo si no existe, leer perfil del sujeto y reutilizar ProfileRules de H03. Sin fila400 PROFILE_REQUIRED. Si ProfileRules.Completeness no es Complete:400 PROFILE_INCOMPLETE, ErrorApiResponse.errors=ProfileRules.ValidateBody del perfil persistido, en el orden H03 y sin valores. No usar un segundo objeto data en el error.
5. Obtener oferta/slot bajo los locks H04, releer estado vigente y reservar atómicamente.

Un replay mantiene la autorización de la sesión actual; no permite reutilizar tokens revocados ni claves de otro sujeto. El bloqueo del actor también serializa la escritura de su perfil H03 y esta lectura, sin añadir un permiso de perfil al personal. Completeness es administrativa: no equivale a aptitud clínica ni habilita check-in/video.

## Contratos API

Envelope y errors:string[] vigentes. Documentar DTO/respuestas tipadas con OpenAPI antes de integrar clientes.

| Ruta existente | Contrato final propuesto |
|---|---|
| POST /api/v1/appointments | Patient; mismo body y header;201 nueva /200 replay; data {id,status,coverageStatus,version} sin cambios |
| GET /api/v1/appointments/me | Patient; array actual, filtros opcionales from/to/status/coverageStatus; máximo100 |
| GET /api/v1/appointments/{id} | Patient propio o Admission; objeto actual con metadatos aditivos indicados abajo |
| GET /api/v1/appointments/{id}/history | Patient propio o Admission; array existente con sequence,oldSlotId,newSlotId,oldStatus,newStatus,oldCoverage,newCoverage,version,actorId,occurredAt |
| POST /api/v1/appointments/{id}/cancel | Patient propio; {version};200 data {cancelled:true} |
| POST /api/v1/appointments/{id}/reschedule | Patient propio; {slotId,version};200 data {rescheduled:true} |
| GET /api/v1/admin/work-queue | Admission; array actual y mismos filtros de listado; máximo200 |
| PATCH /api/v1/admin/appointments/{id}/validation | Admission; CoverageRequest;200 data {status,coverageStatus,version} |

Lista/detalle/cola conservan los campos actuales: id,patientId,patientEmail,slotId,serviceId,practitionerName,serviceName,specialty,startAt,endAt,status,coverageStatus,insurer,memberLast4,version,evidence,reviewedBy,reviewedAt,validUntil. Añaden practitionerId,coverageMethod:"Manual",coverageRequiresReview:boolean,coverageValidityReason:string|null. No ampliar identidades, datos de perfil ni valores clínicos. El historial no incorpora textos de evidencia; los valores anteriores siguen preservados internamente.

Historial y detalle mantienen auditoría de lectura existente. Mostrar evidence solo al sujeto y Admission dentro del alcance ya autorizado, sin enviarla a URL, notificación, telemetría ni capturas de evidencia. No inferir resultados clínicos de los nombres de estados.

### Filtros y límites de listas

from/to se omiten ambos para preservar la consulta histórica actual limitada. Si se suministra uno, deben suministrarse ambos, con offset/Z, intervalo 0 < to-from <=366 días; otro caso400 INVALID_RANGE. Fecha mal formada400 VALIDATION_FAILED. Filtrar startAt>=from y startAt<to.

status opcional acepta exactamente PendingValidation,Confirmed,Cancelled,Rejected. coverageStatus opcional acepta Pending,ManualReview,Approved,Denied,Expired y se aplica sobre la proyección efectiva, antes de LIMIT. Valores vacíos, desconocidos o tipos inválidos400 VALIDATION_FAILED. Orden startAt DESC,id en ambos listados; límites100/200 fijos. No cambiar arrays a páginas ni afirmar que las primeras100/200 contienen todo el histórico. La UI declara el límite y ofrece acotar fechas/estado.

### Cobertura efectiva única

Capturar una vez el instante UTC para la consulta y compartir la misma función/proyección en lista, detalle, cola y filtro. No cambiar status de cita por el paso del tiempo.

Para valor almacenado Approved:
- validUntil null: coverageStatus=ManualReview y coverageValidityReason=MissingValidity.
- validUntil <= ahora: coverageStatus=Expired y coverageValidityReason=Expired.
- validUntil > ahora pero < endAt: coverageStatus=ManualReview y coverageValidityReason=OutsideAppointment.
- validUntil >= endAt y > ahora: coverageStatus=Approved y coverageValidityReason=null.

Para otros valores almacenados conservar coverageStatus y reason=null. coverageMethod=Manual porque este flujo solo registra revisión manual. coverageRequiresReview es true para Pending,ManualReview,Expired y false para Approved,Denied; es un indicador de cobertura, no de aptitud ni permiso de atención. Para cita terminal no ofrecer acciones aunque el indicador sea true.

Un GET no cambia versión, historial, cita, cobertura persistida ni outbox; la auditoría de lectura ya existente se conserva. Una cita puede mostrar Confirmed y Expired a la vez; la UI explica que requiere nueva revisión, sin inferir cancelación, impago o autorización de video.

## Estados y errores de escritura

| Acción válida | Estado cita resultante | Cobertura persistida / efecto |
|---|---|---|
| Nueva solicitud | PendingValidation | ManualReview, version1 |
| Revisión Approved con evidencia/vigencia | Confirmed | Approved; reviewer/fecha/evidencia/vigencia actuales |
| Revisión ManualReview | PendingValidation | ManualReview, vigencia null |
| Revisión Denied | PendingValidation | Denied, vigencia null; no libera slot ni habilita particular |
| Reprogramación | PendingValidation | ManualReview; limpiar evidence,validUntil,reviewedBy,reviewedAt actuales |
| Cancelación | Cancelled | Conservar cobertura histórica; libera capacidad por estado |
| Paso del tiempo | Sin mutación | Solo proyección efectiva en lectura |

Cancelación, reprogramación y revisión admiten exclusivamente PendingValidation/Confirmed y startAt>ahora. No añadir anticipación mínima ni límite de cambios. Revisión de una cita existente no exige que su oferta siga publicada: una desactivación no cancela citas previas ni constituye nueva reserva; preservar el flujo administrativo existente.

Para cancelación/reprogramación/revisión, versión obsoleta y transición/fecha no permitida conservan409 INVALID_TRANSITION. No cambiar esos casos a VERSION_CONFLICT en este incremento. 404 recurso inexistente o cita ajena de Patient; 401 sesión inválida;403 rol no permitido.

Reprogramación toma la cita bajo lock y valida versión/estado antes de usar metadatos. Slot actual nuevamente solicitado:409 SAME_SLOT sin cambio ni aviso. Slot distinto de otro servicio:409 SERVICE_CHANGE_NOT_ALLOWED. Mismo servicio permite otro profesional activo/asociado y slot futuro no bloqueado/no ocupado con publicación H04. Otro destino no disponible:409 SLOT_UNAVAILABLE. Tras éxito no reutilizar evidencia de la hora anterior; el historial conserva lo anterior.

Conservar IDEMPOTENCY_REQUIRED, IDEMPOTENCY_MISMATCH, INSURER_REQUIRED, PROFILE_REQUIRED, SLOT_UNAVAILABLE, INVALID_TRANSITION y EVIDENCE_REQUIRED. Nuevos errores solo PROFILE_INCOMPLETE,SAME_SLOT,SERVICE_CHANGE_NOT_ALLOWED. Binding400 VALIDATION_FAILED conserva su formato seguro sin mensajes SQL ni eco de valores. Una respuesta400/409 no cambia filas funcionales ni crea eventos parciales.

## Orden transaccional y deduplicación

Usar el orden de locks definido en H04: fila del actor y revalidación de sesión mediante AccountGuard (sin nuevo lock de sesión) → idempotencia si procede → cita propia a modificar → servicios por ID → profesionales por ID → asociación releída en sentencia posterior → slots por ID → mutación/historial/evento/outbox/auditoría. No añadir bloqueos globales, reintentos automáticos de mutaciones ni locks de citas ajenas. Revalidar después de esperar cada recurso; lecturas preliminares solo descubren IDs.

Para reprogramar, bajo lock de la cita descubrir servicio y slots antiguo/nuevo, verificar mismo servicio y bloquear servicios/profesionales necesarios antes de ambos slots, ordenados por ID. Releer ocupación del destino con EXISTS sin FOR UPDATE de otra cita; si ocupado,409 antes de UPDATE. Esto evita un ciclo entre dos pacientes intercambiando horarios reservados. Las restricciones PostgreSQL siguen siendo la última defensa contra colisiones y se traducen a SLOT_UNAVAILABLE con rollback.

Cancelación y revisión, que no cambian oferta ni slot, conservan su lock de cita y no adquieren posteriormente locks de catálogo/slots innecesarios. Bloqueo H04 observa ocupación sin lock de cita. Oferta desactivada primero impide reserva/reprogramación posterior; si la reserva confirma primero, se conserva cuando la oferta se desactiva después.

Cita, trigger de historial, idempotencia de creación, auditoría y notificación/outbox deben confirmar juntos. Mantener event_key cita+versión+tipo existente. Replay de Book no crea evento nuevo; dos cancelaciones/reprogramaciones con la misma versión producen una mutación y un409, sin segunda notificación. No se promete nuevo contrato idempotente para cancelación/reprogramación: después de red incierta, recargar explícitamente para comprobar estado.

Un fallo al registrar auditoría/evento revierte la mutación completa. Correo development y fixtures propios en pruebas; no proveedor real ni credenciales en entregables.

## Flujos UX, privacidad y fallos

Flutter conserva repositorio/DTO dentro de appointments y el estado actual. Una nueva solicitud se habilita por profileCompleteness.status=Complete, no por profileVersion>0. Ante PROFILE_REQUIRED/INCOMPLETE dirige a Mi perfil y asocia errores H03 sin descartar la selección de cita. El servidor sigue siendo autoridad.

Antes de POST mostrar resumen con servicio,duración,profesional,fecha America/Santo_Domingo, afiliación enmascarada e intención de solicitar para revisión. Confirmar explícitamente; no presentar como cita confirmada ni cobertura autorizada. Mantener la misma Idempotency-Key y cuerpo exacto para reintentar la intención que pudo llegar al servidor. Tras éxito cerrar esa intención y retirar su clave de la caché activa; una solicitud nueva deliberada usa nueva clave. No eliminar una clave pendiente solo por refrescar la pantalla. Cambiar cuerpo exige una nueva intención/clave después de confirmar que se abandona la anterior, sin reenvío automático.

Mis citas distingue próximas/históricas por fecha, no inventa Completed. Detalle e historial se cargan bajo sesión actual. Cancelación y elección de nuevo horario requieren confirmación; el nuevo horario volverá a revisión. Al409 conservar selección y borrador, ofrecer recargar y no sustituir versión para reintentar automáticamente. Tras éxito recargar lista/detalle autoritativos. Bloquear doble envío y descartar respuestas de filtros/sesiones anteriores.

Portal: filtrar cola en servidor por fecha, cita y cobertura; indicar límite200. Al abrir revisión conservar una copia del borrador de evidencia y versión base;400/red/409 no lo borra. Recargar una cita cambiada exige confirmación de descarte; nunca copiar una nueva versión y enviar evidencia sin revisión del usuario. Detalle/historial solo con los mismos permisos existentes.

Etiquetas contextuales: Approved = Revisión manual favorable; Denied = Revisión manual desfavorable; Expired = Revisión manual vencida. Botón Admission: Registrar revisión favorable y confirmar cita. Indicar que no constituye una autorización recibida de ARS ni determina copago. Estados de cita/cobertura visibles por separado. No modificar globalmente badges de cuentas/otros módulos con los mismos valores.

Logout/expiración detectada limpia citas, borradores y claves pendientes; tokens y datos en memoria, sin nuevas cachés clínicas en disco. Foco, etiquetas, texto escalable y estados loading/empty/error. No rediseño ni correcciones de estilo fuera del trabajo funcional.

## Matriz de autorización

| Actor | Leer cita/historial | Solicitar/cancelar/reprogramar | Cola/revisión manual |
|---|---|---|---|
| Patient activo | Solo propias; ajena404 | Solo propias | 403 |
| Admission activo | Citas operativas con acceso vigente | 403 | Sí |
| Administrator, Practitioner, Support u otro | 403 | 403 | 403 |
| Sesión ausente/revocada/expirada o cuenta inválida | 401 | 401 | 401 |

Permisos clínicos, agenda médica, perfil de dependientes y edición de pólizas no se conceden por este incremento.

## Aceptación Given/When/Then

1. Dado perfil ausente o incompleto, al hacer nueva solicitud se recibe PROFILE_REQUIRED o PROFILE_INCOMPLETE sin reserva/evento; con perfil completo y oferta válida se crea PendingValidation/ManualReview.
2. Dada una reserva previa, cuando se reenvía misma clave literal/cuerpo después de cambiar perfil u oferta, se devuelve200 con respuesta guardada sin duplicación; cuerpo distinto409 y otro sujeto no accede al resultado.
3. Dadas huellas/keys históricas del contrato previo, al repetir la petición se preserva su resultado; no se canonicalizan claves ni se añaden campos al hash.
4. Dados dos pacientes para un slot, cuando compiten hay una reserva y un409, con un historial/evento de creación.
5. Dada cita propia vigente, al reprogramar a otro servicio o mismo slot se recibe409 sin cambiar cobertura, versión ni historial.
6. Dado otro profesional del mismo servicio con oferta válida, al reprogramar se cambia slot/profesional, se reinicia revisión y se conserva historial anterior.
7. Dados dos pacientes intentando intercambiar destinos ocupados, ambos reciben409 sin esperas circulares,500 ni cambios parciales.
8. Dadas cancelaciones concurrentes con la misma versión, una libera capacidad y la otra409; existe una sola notificación del cambio.
9. Dada revisión Approved sin evidencia o vigencia suficiente,400 sin cambios; vigencia igual a endAt y futura es válida, igual a ahora no.
10. Dada revisión Denied/ManualReview tras Approved, al guardar se elimina vigencia favorable actual, se preserva historial y Denied no libera capacidad.
11. Dadas aprobaciones históricas sin vigencia, fuera del fin o vencidas, al leer se aplica la proyección definida sin cambiar versión ni eventos.
12. Dado fallo de auditoría/outbox o carrera revisión/cancelación/reprogramación, se confirma una transición válida o se revierte todo; no se envían avisos de cambios no confirmados.
13. Dados filtros válidos, los listados filtran cobertura efectiva antes del límite y ordenan de forma determinista; filtros inválidos400 sin datos sensibles.
14. Dado error de red/400/409 en Flutter/portal, se conserva intención/borrador; la recarga es confirmada y no hay reenvío automático con nueva versión.
15. Dada sesión nueva o cierre, respuestas viejas no reinstalan datos y el estado privado anterior se limpia.
16. Dados roles y acceso horizontal por HTTP, se cumple matriz; OpenAPI y contratos anteriores siguen disponibles, y regresiones H03/H04/citas pasan con datos sintéticos.

## Verificación prevista y handoff

Pruebas futuras: unidad de proyección/validaciones; HTTP/OpenAPI real y autorización; PostgreSQL para carreras, idempotencia histórica, triggers, rollback y eventos únicos; Flutter repositorio/controlador/widgets; portal Playwright con borrador, conflictos y revisión. Correo development; sin pacientes reales, payloads ni secretos en capturas/logs. No se ejecutaron nuevas pruebas ni se modificó código en esta etapa.

Reutilizar los comandos reales de la [tabla de verificación H04](H04-SPEC-v1.md#verificación-prevista-y-handoff): compilar solución y ejecutar Telemedicina.Tests por dotnet run (suite de consola), flutter analyze --no-fatal-infos y flutter test en app/src, npm run build y npm run test:e2e en portalweb/src. Añadir a la ejecución los casos sintéticos PostgreSQL/HTTP H05 y la regresión H03/H04/citas. No usar dotnet test como sustituto de la suite de consola ni declarar PASS por la sola compilación.

Backend único responsable en api/src; ambos colaboradores Flutter coordinados en app/src; portal en portalweb/src. Revisar contrato OpenAPI antes de clientes. Implementar después de H04 validado, con parche/evidencia H05 separados y revisión posterior Validator/PR.

## Decisiones que requiere aprobar el humano

Aprobar perfil completo solo para nuevas reservas, replay histórico conservado, mismo servicio al reprogramar y semántica de revisión manual/vigencia sin mutación automática. Aprobar conservar la política actual antes de inicio, sin anticipación adicional ni nuevo no-show/particular/ARS/agenda médica/multicanal. Estas exclusiones no equivalen a una decisión clínica, de aseguradora o de validez legal.

La etapa termina en revisión humana de H05-SPEC-v1 y su alcance P0. Antes de Implementer debe existir constancia explícita con versión/huella/fecha; aún no existe. Aprobar H03 o solicitar continuar no sustituye esa aprobación.
