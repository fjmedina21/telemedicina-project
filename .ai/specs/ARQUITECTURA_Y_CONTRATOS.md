# Arquitectura propuesta y contratos transversales

**Estado:** diseño de referencia sujeto a validación. Todas las rutas de API son propuestas en `/api/v1`.

## 1. Componentes

- **Flutter iOS/Android (paciente):** autenticación, perfil, agenda, admisión, videollamada, documentos, seguimiento.
- **Portal web de médico/admisión (mínimo):** agenda, verificación, consulta, documentación, operaciones y soporte. Framework web a elegir en diseño detallado.
- **Backend ASP.NET Core:** monolito modular, REST, control de acceso y auditoría; eventos internos y workers.
- **PostgreSQL** (recomendado; SQL Server viable): persistencia transaccional, migraciones y restricciones.
- **Objeto privado cifrado:** documentos, acceso temporal autorizado y trazabilidad.
- **Proveedores externos:** identidad/OTP, video, notificaciones, ARS (opcional) y pasarela (P1).

**Decisión:** modular monolith primero; evitar microservicios hasta contar con razones demostradas para dividir despliegue, datos y operación.

## 2. Diseño Flutter

Arquitectura orientada a feature con `presentation` (views + view models), `domain` (modelos/reglas y casos de uso cuando aporten) y `data` (repositorios + servicios HTTP). Repositorios como fuentes de verdad. Inspirado en la guía oficial de arquitectura Flutter.

```text
lib/
  app/                 # bootstrap, router, tema y localización
  core/                # network, auth/session, secure storage, errors, telemetry
  shared/              # widgets compartidos, formatos, diseño
  features/
    auth/
    patient_profile/
    specialties/
    appointments/
    coverage/
    check_in/
    video_visit/
    medical_records/
    clinical_documents/
    follow_up/
    notifications/
  main.dart
test/                  # unit y widget tests
integration_test/      # journeys end-to-end
```

**Navegación sugerida:** Inicio, Buscar atención, Mis citas, Mis documentos, Perfil. Acceso a llamada desde la cita autorizada; no ruta pública reutilizable.

## 3. Dominios y agregados

| Dominio | Entidades base | Invariantes |
|---|---|---|
| Identidad | User, Role, Session, LoginChallenge | Un token no concede acceso fuera del sujeto/rol |
| Paciente | Patient, Dependent, EmergencyContact, HealthDeclaration | Paciente identificado; acceso del tutor verificado |
| Oferta | Specialty, Practitioner, Service, Schedule, Slot | Slot pertenece a agenda activa |
| Citas | Appointment, AppointmentHistory | No doble reserva de profesional; cambios auditados |
| Cobertura | InsurancePolicy, EligibilityCheck, Authorization | Elegibilidad no equivale automáticamente a autorización |
| Admisión | CheckIn, ConsentAcceptance, TriageResponse | Consentimiento versionado y check-in asociados a cita |
| Video | VisitSession, JoinToken, ConnectionEvent | Acceso temporal y exclusivo a participantes autorizados |
| Clínica | Encounter, ClinicalNote, Diagnosis, Allergy, Observation | Solo profesional autorizado firma/cierra |
| Documentos | ClinicalDocument, ClinicalDocumentVersion | Archivo vinculado a encuentro y versión inmutable firmada |
| Seguimiento | CareTask, Reminder, Notification, SurveyResponse | Jobs idempotentes; no PHI en push externo |
| Cobros | Invoice, Payment, Claim | Estados de pago separados de los de cita |
| Operación | AuditEvent, SupportIncident | Auditoría sin secretos, integridad y retención definida |

## 4. Máquinas de estado independientes

### Appointment
`Requested → PendingValidation → Confirmed → Completed`  
Alternativas desde estados aplicables: `Cancelled`, `Rejected`, `NoShow`; `Rescheduled` es evento/versión de la reserva, no necesariamente estado terminal. **Regla:** `Completed` requiere encuentro finalizado y conciliación de agenda; pago no determina cierre clínico.

### CoverageCheck
`NotRequired | Pending | Approved | Denied | ManualReview | Expired`. Un `Approved` aplica al servicio y fecha validados; no autoriza automáticamente cualquier servicio.

### CheckIn
`NotStarted → InProgress → Verified → Ready`. Alternativas: `Rejected` o `Expired`; consentimiento e identidad son condiciones antes de `Ready`.

### VideoSession
`Created → Waiting → Active → Ended` con `Interrupted` recuperable y `Failed` terminal. Un rejoin posterior respeta expiración y autorización.

### Encounter
`Draft → InProgress → PendingSignature → Signed → Closed`. Correcciones posteriores: nueva versión/addendum auditado; no sobrescritura de firma.

### Payment
`Pending | Authorized | Paid | Failed | Refunded | Cancelled`; impacto sobre acceso según política aprobada de cobros, nunca sobre documentación ya creada.

## 5. Reglas globales

1. UUID como ID público; timestamps UTC en persistencia, visualización America/Santo_Domingo con zona correcta en API (`ISO 8601`).
2. Paciente solo accede a su información y a dependientes con vínculo verificado; médicos, solo a pacientes dentro de una relación de atención autorizada.
3. Acciones con efecto económico o de reserva soportan `Idempotency-Key`; agenda valida colisiones mediante transacción y restricción en BD.
4. Cambios de estado usan política de transición explícita; errores `409 Conflict` para concurrencia y transición inválida.
5. Documentos sensibles se descargan autenticados o con enlaces temporales de corto plazo; no aparecen en notificaciones abiertas.
6. `AuditEvent` registra actor, acción, recurso, resultado, momento y contexto mínimo; nunca contraseñas, tokens, texto clínico completo o secretos.
7. Guardar `ConsentAcceptance` con versión y tiempo; consentimiento clínico y permisos de privacidad se modelan por separado.
8. Datos sanitarios, identidad, localización, imágenes y documentos solo se recopilan para un propósito justificado.
9. Procesos asíncronos usan outbox/reintentos con backoff y deduplicación; nadie debe recibir dos recordatorios por mismo evento.
10. No hay diagnóstico ni decisiones de urgencia autónomos; alertas de triage se definen y revisan clínicamente.

## 6. API y respuesta estándar

**Formato sugerido:** JSON y HTTPS; documentación OpenAPI. Error RFC 7807 `application/problem+json` con `type`, `title`, `status`, `detail` no sensible, `code`, `traceId`. Listados paginados, filtros validados, `ETag`/versión en operaciones concurrentes.

**Endpoints base sugeridos:**

```text
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh
GET    /api/v1/patients/me
PATCH  /api/v1/patients/me
GET    /api/v1/specialties
GET    /api/v1/practitioners?specialtyId=...
GET    /api/v1/availability?practitionerId=...&from=...&to=...
POST   /api/v1/appointments
GET    /api/v1/appointments/me
POST   /api/v1/appointments/{id}/cancel
POST   /api/v1/appointments/{id}/reschedule
POST   /api/v1/coverage-checks
GET    /api/v1/coverage-checks/{id}
POST   /api/v1/appointments/{id}/check-in
POST   /api/v1/appointments/{id}/consents
POST   /api/v1/appointments/{id}/video-token
GET    /api/v1/encounters/{id}
POST   /api/v1/encounters/{id}/notes
POST   /api/v1/encounters/{id}/sign
GET    /api/v1/encounters/{id}/documents
GET    /api/v1/documents/{id}/download
GET    /api/v1/follow-ups/me
POST   /api/v1/notifications/{id}/read
```

**Ejemplo de error, no definitivo:**

```json
{
  "type": "https://api.example.invalid/problems/slot-unavailable",
  "title": "Horario no disponible",
  "status": 409,
  "code": "APPOINTMENT_SLOT_UNAVAILABLE",
  "traceId": "7b0c..."
}
```

## 7. Integración sanitaria (preparar, no imponer)

Preparar traducciones de los modelos internos a recursos HL7 FHIR cuando se definan integraciones: `Patient`, `Practitioner`, `Appointment`, `Encounter`, `Coverage`, `Consent`, `MedicationRequest`, `ServiceRequest` y `DocumentReference`. No afirmar conformidad FHIR hasta definir perfiles, terminologías, versionado y pasar pruebas con sistemas concretos.

## 8. Seguridad, privacidad y operación

- MFA para personal clínico/administrativo, autorización en servidor por rol y relación de atención, revocación de sesiones y protección anti-enumeración/rate limit.
- Tokens solo en almacenamiento seguro del sistema; evitar guardar expedientes completos en caché local y borrar datos sensibles al cerrar sesión.
- TLS, secretos fuera del binario, cifrado en reposo, respaldos probados, retención/depuración aprobadas, audit trail.
- Separación dev/staging/prod y datos sintéticos fuera de producción; no incluir PII/PHI en crashes/analytics.
- Política de consentimiento de cámara/micrófono, aviso de uso y control de grabación explícito (desactivada por defecto).
- Estudiar MASVS de OWASP para el cliente móvil y una evaluación legal/local para datos de salud, telemedicina, recetas y firma.
- Observabilidad: métricas de reservas, errores HTTP, latencia de join/rejoin, notificaciones y jobs; alertas sin exposición de datos clínicos.

## 9. Dependencias tecnológicas a decidir en un spike

- Estado Flutter: `ChangeNotifier`/Riverpod/BLoC, seleccionando uno y fijando convenciones.
- Navegación declarativa, DI, cliente HTTP tipado, serialización y localización; evaluar sus versiones antes de instalarlos.
- SDK de videollamada compatible con Flutter, requisitos de privacidad, reconexión y capacidad de obtener token desde API.
- Push: APNs/FCM; correo/WhatsApp mediante adaptadores server-side, sin incluir contenido sensible.
- Hosting, backups, logs, secretos y pipelines compatibles con presupuesto y políticas del prestador.
