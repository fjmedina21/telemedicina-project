# Diseño técnico M02 — Hito 03

**Versión propuesta:** H03-DESIGN-v1  
**Estado:** propuesta pendiente de aprobación humana; no implementado.  
**Alcance:** perfil propio, contacto de emergencia y declaraciones autodeclaradas. Dependientes P1 y portal condicionado.

## Contexto y decisiones

La base del hito ya existe:

- `Features/Patients/PatientsController.cs` implementa `GET/PATCH /api/v1/patients/me`, restringidos al rol `Patient`.
- `PatientsContracts.cs` define `ProfileRequest` con nombres, nacimiento, teléfono, dirección, contacto de emergencia, tres listas (`allergies`, `medications`, `conditions`) y `version`.
- El perfil se almacena en `profiles.body`; la lectura devuelve `profile`, `version`, `updatedAt` y `source: SelfDeclared`.
- La escritura usa transacción, bloqueo asesor por paciente, `FOR UPDATE`, validación de versión y evento `profile.updated`.
- Flutter dispone de `PatientProfileForm` y `PatientController.saveProfile`; ya distingue declaraciones de diagnósticos.
- El formulario actual usa texto separado por comas y mensajes globales; no conserva metadatos de procedencia separados por sección.

Propuesta: ampliar este contrato y flujo de forma compatible. No crear endpoints de declaraciones ni un segundo almacenamiento que duplique la información incluida en el perfil.

## Componentes e interacciones

API: reglas, completitud, procedencia, timestamps, autorización, concurrencia y auditoría.

Flutter: reutilizar `ChangeNotifier`, cliente HTTP y sesión existentes. Incorporar repositorio de perfil dentro de `features/patient_profile/data/` y modelos del contrato dentro de la feature. Mantener `PatientController` como coordinador del shell mediante ese repositorio; no introducir otro gestor de estado.

El modelo `core/models/patient.dart` tiene nombres y valores por defecto distintos del contrato real; no usarlo para interpretar esta respuesta sin adaptación explícita.

Portal: este incremento no concede acceso adicional a perfiles. Posponer pantallas y correcciones administrativas hasta aprobar tareas, campos y permisos; los roles administrativos siguen sin acceso a `/patients/me`.

## Modelo de datos

Mantener una fila por paciente y el `body` actual. Mantener campos y límites vigentes:

| Campo | Regla propuesta |
|---|---|
| firstName, lastName | Obligatorios, recortados, máximo 80 caracteres |
| birthDate | Obligatoria, año >=1900 y anterior a hoy; sin inferir habilitación clínica por edad |
| phone, emergencyPhone | Obligatorios, formato de teléfono y máximo 30 |
| address | Obligatoria, máximo 250 |
| emergencyName | Obligatoria, máximo 100 |
| Tres listas de declaraciones | Máximo 20 elementos por lista; cada elemento no vacío, máximo 200 |
| version | Entero >=0; 0 para creación |

Estas reglas preservan validación vigente; confirmar obligatoriedad para el piloto en aprobación del diseño. Lista vacía significa “sin datos registrados”, no negación de alergias.

Añadir tres timestamps nullable a `profiles`: `allergies_updated_at`, `medications_updated_at`, `conditions_updated_at`. El servidor actualiza solo la categoría cuyos valores cambian. Fuente `SelfDeclared`; el cliente no puede marcar información clínicamente validada.

Migración aditiva e idempotente según mecanismo SQL existente. Filas anteriores conservan NULL en nuevos campos porque la fecha histórica específica no se conoce. `updated_at` global sigue representando última modificación del perfil completo. No atribuir fecha nueva a declaraciones históricas durante migración.

## APIs y contratos

Conservar envelope `ApiResponse` y rutas existentes. PATCH conserva semántica actual de envío de perfil completo; documentarla expresamente.

GET `/patients/me` conserva `data: null` cuando no existe perfil y añade, cuando existe:

- `declarationMetadata`: categoría -> `{source: "SelfDeclared", updatedAt: timestamp|null}`.
- `completeness`: `{status: "Complete"|"Incomplete", missingFields: string[]}`.

Proponer GET `/api/v1/patients/me/completeness` con datos mínimos para indicar campos faltantes incluso sin perfil. No expone sus valores. Check-in futuro reutilizará servicio de completitud; este hito no lo habilita.

PATCH inicializa timestamps de categorías con contenido en creación. En ediciones compara valores normalizados dentro de la transacción. Conserva respuesta `{version}`; Flutter recarga perfil.

Errores propuestos:

- 400 INVALID_PROFILE: campos inválidos, `errors` por campo sin eco de valores.
- 401: sesión ausente o inválida.
- 403: rol no habilitado.
- 409 VERSION_CONFLICT: recargar antes de otra edición.
- Rechazar explícitamente campos protegidos de identidad enviados por el cliente; no ignorarlos silenciosamente.

Preservar formato real de `ApiErrors` y documentar correspondencia con Problem Details antes de modificarlo.

## Seguridad y privacidad

Resolver paciente desde sesión, nunca desde un ID enviado por cliente. Mantener AccountGuard y auditoría en transacción de escritura. Auditar metadatos sin valores anteriores/nuevos de declaraciones.

`phone` es contacto autodeclarado: cambiarlo no lo convierte en medio verificado ni modifica credenciales. Email e identificadores de acceso permanecen en M01.

No añadir documento legal, estado verificado ni mecanismos de corrección al formulario. El perfil no escribe esos recursos. UI puede informar que corrección de identidad requiere canal del centro, sin afirmar que existe trámite automatizado.

## Estados, errores y concurrencia

Conservar versión entera y bloqueos vigentes, sin introducir ETag simultáneamente. Dos solicitudes con misma versión solo permiten una escritura válida. No reintentar conflictos automáticamente con datos obsoletos.

Flutter conserva borrador en memoria ante red o conflicto. No sustituir campos durante edición sin confirmar. Tras 409, ofrecer recargar avisando pérdida del borrador. Cierre o expiración de sesión limpia borradores y datos privados.

Usar entradas individuales eliminables y limitadas para declaraciones, permitiendo comas dentro de elementos. Mostrar fuente y fecha por sección; indicar fecha desconocida cuando corresponda.

## Plan de pruebas

Validación futura con datos sintéticos:

1. Creación, edición, recarga y persistencia tras reinicio.
2. Sesión ausente, roles de personal y manipulación de identidad.
3. Concurrencia: una sola versión aceptada.
4. Auditoría atómica sin contenido clínico.
5. Migración histórica sin pérdida ni fechas inventadas.
6. Completitud ausente/incompleta/completa y errores por campo.
7. Metadatos cambian solo en categoría editada.
8. Flutter: borrador ante error, conflicto, expiración, accesibilidad, ausencia de cache/logs sensibles.
9. Regresión de citas que consumen perfil.

No se ejecutaron pruebas ni se modificó código durante esta etapa.

## Handoff al Spec Creator

Convertir propuestas en historias API/Flutter y criterios positivos/negativos con ejemplos y autorización. Obtener aprobación explícita de obligatoriedad de campos, semántica de listas vacías, metadatos e históricos, completitud sin admisión, portal sin permisos adicionales y conservación de identidad existente.

## Contrato API para coordinación frontend/backend

Publicar OpenAPI y ejemplos sintéticos antes de integrar Flutter. Backend controla implementación y migración en `api/src/`; ambos colaboradores coordinan Flutter en `app/src/`. Portal condicionado. Mantener campos y envelope compatibles: clientes anteriores guardan perfiles sin enviar los nuevos metadatos calculados por servidor.
