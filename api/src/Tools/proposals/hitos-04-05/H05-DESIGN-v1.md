# Diseño técnico — Hito 05: citas y revisión manual de cobertura

**Versión:** H05-DESIGN-v1.  
**Estado:** propuesta para Spec Creator y revisión humana; no implementada ni aprobada.  
**Módulos:** M04 y M05, incremento P0 conservador.  
**Dependencias:** H03 completado, contrato/política común de oferta H04 aprobado y aplicado antes de integrar H05.

## Contexto y decisiones

Ya existen solicitud Patient con Idempotency-Key UUID, transacción, exclusión de reservas por profesional, historial append-only, cancelación, reprogramación, cola Admission, revisión manual con evidencia/vigencia, notificaciones y outbox deduplicados. Solicitud crea PendingValidation + ManualReview; revisión manual Approved permite Confirmed; Denied permanece PendingValidation y no libera el slot. Reprogramación reinicia cobertura y revisión. No añadir otro sistema de citas/cobertura paralelo.

Brechas verificadas: Book solo exige existencia de fila de perfil, sin comprobar completitud H03. Reschedule admite otro servicio mediante llamada directa aunque Flutter filtre por servicio original. AppointmentQueries solo calcula Expired cuando coverage_valid_until<now y no define explícitamente el caso Approved sin vigencia. UI usa Aprobada y Aprobar cobertura y confirmar sin distinguir resultado manual de autorización ARS. Flutter no ofrece detalle/historial ni una resolución explícita de conflictos de citas. Las políticas de anticipación, no-show, particular y agenda profesional no tienen especificación aprobada.

**Propuesta de alcance P0 para aprobación:** conservar políticas operativas actuales (solicitar solo slot futuro; cancelar/reprogramar solo PendingValidation/Confirmed antes de inicio; sin umbral adicional), completar validación y coherencia transaccional, bloquear cambio de servicio por reprogramación, mostrar detalle/historial y estados separados, y hacer explícita fuente manual y vigencia de cobertura. No implementar tarifas, pagos/particular, conectores ARS, pólizas completas, no-show, cierre clínico, registro telefónico por personal ni agenda Practitioner sin autorización específica. El cierre P0 no debe declararse implementación integral de US-12 a US-19 ni M04/M05 completos.

## Componentes e interacciones

API: reutilizar AppointmentsController, AppointmentQueries, AdmissionController y NotificationService. Compartir ProfileRules de H03 y política común de oferta de H04. Contratos tipados/OpenAPI antes de clientes.
Flutter: repositorio/DTO de appointments dentro de la feature; selección conservada de H04; resumen explícito antes de solicitar, validación de aseguradora/últimos4, detalle e historial propio, estados próximos/históricos sin inventar Completed. Versiones vigentes, confirmación de cancelación y nueva hora, manejo409 sin reintento automático. Sustituir la condición hasProfile=version>0 por completeness.status=Complete de H03 para habilitar una nueva solicitud; el servidor sigue siendo autoridad.
Portal Admission: cola actual, filtro de cobertura, detalle/historial mínimo necesario y formulario de revisión manual. Conservar borrador de evidencia ante400/red/409 y pedir recarga explícita si cambió la versión. No conceder nuevas operaciones de perfil ni acceso clínico al personal.

## Datos y validaciones

No crear nuevas entidades de póliza, Authorization ni proveedor. appointments conserva insurer y member_last4 enmascarados en UI; no solicitar afiliación completa. No introducir motivo clínico libre sin propósito/revisión de datos.

BookRequest conserva slotId UUID, insurer obligatorio recortado máximo100 y memberLast4 exactamente cuatro dígitos ASCII. Idempotency-Key UUID requerido como hoy. Conservar exactamente BookRequest, el algoritmo y la serialización de la huella actuales, y la representación existente de la clave; no añadir campos nullable ni nueva normalización que invalide replays persistidos. No recalcular ni borrar hashes antiguos. La normalización para almacenamiento conserva las reglas actuales; cualquier revisión futura de clave/huella requiere un plan compatible independiente.

Para nueva reserva, leer perfil del sujeto desde BD y aplicar ProfileRules.Completeness. Sin fila mantiene PROFILE_REQUIRED; fila incompleta devuelve400 PROFILE_INCOMPLETE y data no sensible con missingFields, o errors campo:REQUIRED/INVALID en ErrorApiResponse vigente. Spec Creator elegirá una única forma coherente con H03, sin valores del perfil. Esta regla no determina aptitud clínica ni habilita check-in.

RescheduleRequest conserva slotId y version>=1. Debe cambiar a un slot diferente del actual y del mismo serviceId original; otro profesional del mismo servicio sí puede seleccionarse si activo y asociado. Otro servicio produce409 SERVICE_CHANGE_NOT_ALLOWED y no altera cita/revisión/historial. Repetir slot actual produce409 SAME_SLOT y no reinicia cobertura ni notifica.

CoverageRequest conserva Status Approved|Denied|ManualReview, Evidence recortada obligatoria máximo250, ValidUntil y Version>=1. Approved requiere vigencia no nula que cubra endAt inclusive y sea futura al confirmar. Denied/ManualReview limpian validUntil para evitar una vigencia favorable residual; evidencia y reviewer/checkedAt se guardan siempre. Una referencia escrita por Admission es evidencia administrativa, no verificación del asegurador.

## APIs y contratos

Mantener envelope success/status/detail/data y errors:string[]; no sustituirlo globalmente. Todas las rutas de esta tabla ya existen; los cambios se proponen como ampliación o endurecimiento explícito:

| Ruta | Resultado/semántica propuesta |
|---|---|
| POST /api/v1/appointments | Mantiene201 nueva y200 replay; data {id,status:PendingValidation,coverageStatus:ManualReview,version:1}; completar perfil y oferta vigente dentro de transacción |
| GET /api/v1/appointments/me | Mantener array y campos existentes; filtros opcionales from/to/status/coverageStatus validados; orden determinista startAt DESC,id; límite100 visible en UI, sin afirmar totalidad de archivo histórico |
| GET /api/v1/appointments/{id} | Propio Patient o Admission; conservar respuesta y añadir practitionerId, coverageMethod:Manual, coverageRequiresReview:boolean, coverageValidityReason nullable seguro |
| GET /api/v1/appointments/{id}/history | Conservar historial existente y autorización; presentar eventos de reserva/cambio/cancelación/revisión sin datos clínicos ni valores de evidencia |
| POST /api/v1/appointments/{id}/cancel | Mantiene {version}; transición actual, una sola mutación/evento, sin auto-retry; tras éxito recargar detalle/lista |
| POST /api/v1/appointments/{id}/reschedule | Mantiene {slotId,version}; mismo servicio, slot distinto, futura oferta válida; reinicia ManualReview y borra evidencia/vigencia/reviewer de la revisión anterior en estado actual; historial conserva lo anterior |
| GET /api/v1/admin/work-queue | Mantiene array; filtros opcionales de fecha/estado/coverageStatus para no filtrar solo primeras200; preservar límite200 documentado |
| PATCH /api/v1/admin/appointments/{id}/validation | Mantiene request y response {status,coverageStatus,version}; validar versión, fecha y evidencia/vigencia; campos de estado resultan del servidor |

Detalle/lista/cola deben compartir una única proyección de cobertura. coverageStatus conserva significado efectivo: Approved solo si existe vigencia que cubre la cita y es posterior al instante UTC actual; si aprobación tiene vigencia ya vencida, Expired; si falta vigencia o no cubre fin de cita, ManualReview con coverageValidityReason MissingValidity|OutsideAppointment. Para otros estados conservar valor guardado. coverageMethod=Manual en este flujo. coverageRequiresReview es true para Pending/ManualReview/Expired. El servidor no cambia status de cita, versiones, historial ni outbox durante un GET.

La cita puede continuar Confirmed con revisión Expired: mostrar ambos estados y aviso de nueva revisión; no inferir cancelación, impago, exclusión clínica ni acceso a video. No migrar silenciosamente citas históricas ni atribuir evidencia faltante. Toda acción que confirme de nuevo necesita versión vigente y nueva revisión válida.

Errores: conservar IDEMPOTENCY_REQUIRED, IDEMPOTENCY_MISMATCH, PROFILE_REQUIRED, SLOT_UNAVAILABLE, INVALID_TRANSITION, EVIDENCE_REQUIRED; añadir PROFILE_INCOMPLETE, SERVICE_CHANGE_NOT_ALLOWED y SAME_SLOT. Para distinguir conflicto de versión de transición usar VERSION_CONFLICT de forma coordinada o conservar INVALID_TRANSITION con causa segura acordada en spec. 401/403 roles/sesión; 404 al pedir cita ajena como Patient, sin revelar existencia. No devolver excepciones SQL, payloads, tokens ni valores clínicos.

## Estados y reglas a aprobar

| Acción | Cita | Cobertura |
|---|---|---|
| Nueva solicitud | PendingValidation | ManualReview |
| Revisión manual Approved válida | Confirmed | Approved efectivo mientras válida |
| Revisión ManualReview | PendingValidation | ManualReview |
| Revisión Denied | PendingValidation (mantener comportamiento actual) | Denied; no particular ni liberación automática |
| Reprogramación autorizada | PendingValidation | ManualReview, revisión anterior invalidada |
| Cancelación autorizada | Cancelled | Se conserva registro histórico de cobertura, no se reutiliza |
| Paso del tiempo sin acción | Estado de cita sin mutación | Proyección efectiva Expired si corresponde |

Los estados de cita y cobertura son independientes. No crear Completed ni NoShow sin encuentro/política aprobados. Cancelación y reprogramación no requieren umbral inventado: solamente antes de startAt conforme a la base. Si el humano quiere anticipación mínima, límites de cambios, particular o no-show, registrar valores y reglas en una nueva versión aprobada antes de implementarlos. H05-SPEC-v1 debe presentar expresamente esta decisión de alcance conservador.

## Seguridad, transacciones e idempotencia

Patient solo solicita y modifica su cita; Admission revisa y consulta el mínimo operacional que ya tiene. Practitioner/Support/Administrator siguen sin acceso de citas/cobertura por defecto. Resolución de identidad desde sesión; AccountGuard dentro de escrituras. Nunca aceptar createdBy/userId desde el cuerpo.

Conservar orden usuario antes de sesiones/recursos de AccountGuard, bloqueo de idempotencia por usuario+clave existente, filas de cita/slot y restricciones PostgreSQL. Comprobación de idempotencia debe ocurrir antes de nuevos requisitos del perfil o de oferta para que un replay válido de una reserva ya creada no se convierta en un segundo intento ni falle porque cambió la oferta.

Para cada operación nueva, tomar locks de recursos de oferta y comprobar asociación en una sentencia posterior, como H04; no confiar en lectura preliminar de disponibilidad. Reschedule debe releer servicio original y versión bajo lock de cita y adquirir slots necesarios en orden determinista; definir un orden común verificable con catálogo/bloqueo para evitar deadlocks. No hay que exigir ausencia de toda concurrencia: asegurar un único cambio válido y rollback íntegro cuando aparece conflicto.

Auditoría, historial, cita, idempotencia y eventos/outbox se confirman atómicamente. Un409 o validación400 no genera cambios parciales ni aviso. Mantener event_key único por cita/versión/tipo; replay de Book no emite nueva notificación. Reintento de cancelación con versión antigua puede devolver409 y el cliente recarga para comprobar el resultado; no se promete nuevo contrato idempotente de cancelación. Correo real permanece fuera del incremento y pruebas fuerzan development.

## UX y privacidad

Revisión Approved se rotula Revisión manual favorable; Denied, Revisión manual desfavorable; nunca Autorizado por ARS. Botón portal: Registrar revisión favorable y confirmar cita. Describir que cobertura/revisión y cita tienen estados separados y que una revisión vencida requiere gestión administrativa. No publicar copagos, particular o confirmaciones económicas inventadas.

Resumen de solicitud: servicio, duración, profesional, fecha local/UTC estable y datos mínimos de afiliación enmascarados; confirmación de intención antes de POST. Conservar Idempotency-Key de la misma solicitud ante red; una nueva intención tras cancelar/refrescar explícitamente utiliza nueva clave. Tras éxito no reutilizar indefinidamente la clave previa para reservar de nuevo el mismo slot en el futuro. Estado loading y bloqueo de doble envío.

Ante409, conservar intención/borrador y ofrecer recargar opciones o detalle; nunca reemplazar versión y reintentar automáticamente. Tras guardado recargar estado autoritativo. No instalar respuestas de sesiones anteriores. Logout/expiración detectada limpia datos y borradores. Detalle/historial accesibles, tiempos America/Santo_Domingo y UTC consistente; sin PHI en notificaciones, logs, URLs ni capturas.

## Plan de pruebas (propuesto; no ejecutado)

1. HTTP matriz completa de roles, 404 ajeno, sesión revocada y manipulación userId/estado; sin nuevos permisos.
2. Perfil ausente/incompleto/completo, faltantes seguros y replay de idempotencia tras cambiar perfil/oferta.
3. Dos pacientes, mismo slot: una reserva; misma clave/cuerpo replay único; distinto cuerpo409 y notificación única; compatibilidad con respuestas y hashes históricos de solicitudes previas.
4. Reprogramación entre servicios rechazada; mismo slot sin efectos; otro profesional del mismo servicio válido; reset revisión y preservación de historial.
5. Cancelación/liberación, versión obsoleta, doble petición y outbox exactamente una vez.
6. Coverage Approved requiere evidencia/vigencia hasta endAt; fronteras UTC, vencimiento en lectura y ausencia histórica de vigencia; GET sin mutación de versión/eventos.
7. Carreras revisión/cancelación/reprogramación/cambio oferta, rollback ante fallo de auditoría y sin pérdidas parciales.
8. Flutter widgets/controlador: resumen, idempotencia ante red, detalle/historial, 409, etiquetas manuales y estados separados. Portal Playwright: evidencia conservada, recarga explícita y denegación de roles.
9. Regresión H01-H04 y contratos OpenAPI, datos sintéticos, SMTP real deshabilitado.

## Archivos previstos y handoff al Spec Creator

API: AppointmentsContracts.cs, AppointmentsController.cs, AppointmentQueries.cs, AdmissionContracts.cs, AdmissionController.cs, DTO/OpenAPI y pruebas/runner. Reutilizar reglas H03/H04. Preferir sin migración adicional salvo justificar un dato nuevo imprescindible; no alterar tablas clínicas ni historial append-only.
Flutter: appointments data/domain/presentation, BookingForm, PatientController/PatientShell, etiquetas de cobertura contextualizadas y tests; cambios de feature catalog solo para consumir H04. Portal: AdmissionQueue, PortalConsole y cliente/repositorio/tipos; no rediseño global ni nuevas pantallas clínicas.

Producir H05-SPEC-v1 después de H04-SPEC-v1 con historias y casos verificables. Aprobación explícita requerida para: perfil completo antes de nueva reserva, mismo servicio al reprogramar, semántica manual/vigencia, preservación de estados y ausencia de nuevas políticas de anticipación/no-show/particular. Documentar exclusión de US-14, US-15 y US-19 y partes no autorizadas de US-12/US-16 como alcance futuro; no fingir cumplimiento total del módulo.

## Contrato API para coordinación

El contrato backend se revisa antes de actualizar app y portal. Backend único responsable, Flutter compartido y portal por área. H05 se implementa y valida después del incremento H04; su aprobación no se infiere de aprobar H03 ni de esta solicitud de continuar. Esta etapa produce diseño, no código ni pruebas ejecutadas.
