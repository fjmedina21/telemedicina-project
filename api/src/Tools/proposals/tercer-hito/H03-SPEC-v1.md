# Spec ejecutable M02 — Hito 03

**Versión:** H03-SPEC-v1  
**Estado:** propuesta pendiente de revisión humana; no autoriza implementación.  
**Entrada:** H03-PLAN-v1 aprobado y handoff Architect H03-DESIGN-v1.  
**Alcance:** perfil propio P0; dependientes P1; portal condicionado.

## Base existente y resultado

`PatientsController` ya ofrece GET/PATCH `/api/v1/patients/me` para rol Patient. `ProfileRequest` contiene contacto, emergencia, tres listas de declaraciones y versión. La escritura usa transacción, bloqueo por paciente, control de versión y `profile.updated`. Flutter ya tiene formulario y guardado. Se propone completar esta base, sin endpoints separados para declaraciones ni duplicación de datos.

## Historias de usuario

| ID | Área | Resultado verificable | Trazabilidad |
|---|---|---|---|
| H03-01 | API | Validar y guardar perfil propio con versión y errores por campo | US-05 |
| H03-02 | API | Conservar fuente y fecha por categoría de declaración | US-06 |
| H03-03 | API | Informar campos obligatorios faltantes sin valores | M02 aceptación 4 |
| H03-04 | Flutter | Editar contacto y emergencia con estados claros | US-05 |
| H03-05 | Flutter | Editar listas por elemento, ver fuente/fecha y resolver errores/conflictos | US-06 |
| H03-06 | API/Flutter | Mantener aislamiento, identidad protegida y borrado de estado privado | Reglas M02/M01 |

US-07 y US-08 no se implementan aquí: dependientes y corrección/fusión administrativa necesitan especificaciones posteriores.

## Datos y validaciones propuestas

Todas las cadenas se recortan en servidor antes de validar y guardar. Rechazar tipos incorrectos, cadenas obligatorias vacías y listas nulas. Mantener los límites actuales:

| Campo | Regla |
|---|---|
| firstName, lastName | Obligatorios, máximo 80 caracteres cada uno |
| birthDate | ISO AAAA-MM-DD, año >=1900 y anterior al día UTC actual |
| phone, emergencyPhone | Obligatorios, máximo 30; mismo validador Phone del backend actual |
| address | Obligatoria, máximo 250 |
| emergencyName | Obligatorio, máximo 100 |
| allergies, medications, conditions | Cada lista obligatoria, admite []; máximo 20 elementos; cada elemento recortado no vacío, máximo 200 |
| version | Entero >=0, 0 para creación |

No inferir aptitud clínica por edad. Una lista vacía significa “Sin datos registrados”; no equivale a negar alergias ni antecedentes. Comparar listas recortadas conservando orden, duplicados y mayúsculas: no introducir normalización clínica ni deduplicación semántica.

El teléfono es contacto autodeclarado; su edición no verifica el número ni cambia credenciales. No permitir que este contrato escriba `userId`, `patientId`, `documentType`, `documentNumber`, `identificationCard`, `verifiedAt`, email, roles o estado de cuenta. Si esas propiedades se envían, responder 400 `INVALID_PROFILE`, sin eco de su valor. No añadir identificadores legales al formulario.

## Contratos de API

Mantener el envelope actual `success`, `status`, `detail`, `data`; documentar cambios aditivos en OpenAPI antes de integrar Flutter.

### GET /api/v1/patients/me

200 conserva `data: null` si no hay fila. Si existe, conserva `profile`, `version`, `updatedAt`, `source: SelfDeclared` y agrega:

- `declarationMetadata`: objeto con claves `allergies`, `medications`, `conditions`; cada valor contiene `source: SelfDeclared`, `updatedAt: ISO UTC|null`.
- `completeness`: `{status: Complete|Incomplete, missingFields: string[]}`.

`profile` mantiene nombres actuales. La versión exterior es autoritativa; no usar una versión histórica que pudiera estar serializada dentro de `body`.

### PATCH /api/v1/patients/me

Conservar envío de perfil completo y todos los campos de ProfileRequest; no es un merge parcial. Metadatos y completitud son calculados por servidor, no editables. 200 conserva `data: {version: nuevaVersion}`. Cada guardado válido incrementa versión como ahora; un guardado sin cambios no altera fechas de categorías.

Mantener la transacción y bloqueos existentes. Solo se confirma la escritura si la versión coincide; de lo contrario 409 `VERSION_CONFLICT`, sin sobrescribir ni reintentar automáticamente. Auditoría y perfil se confirman juntos; no incluir valores clínicos en auditoría.

### GET /api/v1/patients/me/completeness

Nuevo endpoint Patient-only. 200: `data: {status: Complete|Incomplete, missingFields: string[]}`. Campos candidatos, en orden estable: firstName, lastName, birthDate, phone, address, emergencyName, emergencyPhone, allergies, medications, conditions. Una lista presente y vacía satisface presencia; null o ausencia no. Sin perfil devuelve Incomplete con todos. Para datos históricos inválidos, listar campos ausentes o inválidos, sin sus valores. Esto no habilita check-in ni establece políticas clínicas.

### Errores

Mantener `ErrorApiResponse` y su lista `errors: string[]`; no reemplazarla por un diccionario. Para validaciones de perfil usar entradas estables `campo:REQUIRED`, `campo:INVALID`, `campo:MAX_LENGTH` o `campo:MAX_ITEMS`, sin valores. 400 conserva `VALIDATION_FAILED` para binding/annotations y `INVALID_PROFILE` para reglas del perfil; ambos suministran errores por campo cuando identificable. 401 sesión ausente/inválida, 403 rol no permitido, 409 conflicto. No cambiar globalmente errores de otros módulos.

## Persistencia y migración

Mantener una fila y body existentes. Agregar a `profiles` columnas nullable timestamptz: allergies_updated_at, medications_updated_at, conditions_updated_at mediante migración aditiva idempotente. No reemplazar filas ni rellenar fechas históricas.

Crear fechas de categorías inicialmente no vacías al guardar un perfil nuevo. En filas existentes, cambiar solo fecha de categoría cuyos valores normalizados cambien, incluso al vaciarla; usar hora del servidor en UTC dentro de la transacción. Si una categoría histórica no cambia, su fecha sigue null. Fuente siempre SelfDeclared. El updated_at global conserva su significado actual.

## Flujos UX y fallos

Reutilizar navegación, sesión, cliente HTTP y gestión de estado existentes; repositorio y DTO de perfil dentro de la feature. No usar el modelo genérico Patient con campos y valores por defecto incompatibles sin adaptador explícito.

Cargar perfil y completitud; mostrar pendientes, datos personales, emergencia y declaraciones. Usar entradas individuales agregables/eliminables, permitiendo comas dentro de un elemento. Mostrar “Autodeclarado” y fecha por categoría; null se presenta como “Fecha no disponible”.

Mientras guarda, deshabilitar envío duplicado. Ante 400, asociar errores al campo y conservar borrador en memoria. Ante fallo de red, conservar borrador y permitir reintentar; si el servidor sí guardó, el reintento puede producir 409. Ante 409, avisar y ofrecer recarga confirmada que descarta borrador; nunca sobrescribir automáticamente. En éxito, recargar y mostrar confirmación. No sustituir borrador por actualizaciones externas sin aviso. En logout/expiración, limpiar datos privados y borrador; no persistirlos en disco. Mantener accesibilidad, foco y texto escalable.

## Matriz de autorización

| Actor | Leer/guardar perfil y completitud |
|---|---|
| Patient activo y sesión válida | Solo propio, derivado de la sesión |
| Sin sesión, sesión revocada/expirada | Denegado por guardas actuales |
| Admisión, médico, soporte, administrador | Sin nuevo permiso; denegado en estas rutas si no tienen rol Patient |
| Tutor/dependiente | No habilitado |

Portal no incorpora pantalla ni permiso de perfil en este incremento. La asignación futura de permisos requiere revisión independiente.

## Criterios Given/When/Then

1. Dado paciente sin perfil, cuando guarda datos válidos con versión 0, entonces GET devuelve versión 1, datos persistidos, fuente y fechas de categorías no vacías.
2. Dado otro paciente autenticado, cuando consulta /me, entonces solo obtiene su perfil; cuando envía patientId ajeno para modificarlo, entonces recibe 400 sin cambio de ninguno.
3. Dadas dos escrituras con la misma versión, cuando compiten, entonces solo una confirma y la otra recibe 409, con un único incremento y auditoría de escritura confirmada.
4. Dada una categoría histórica sin fecha, cuando solo cambia teléfono, entonces su fecha sigue null; cuando cambia la categoría, entonces recibe fecha del servidor.
5. Dada lista vacía, cuando se muestra, entonces dice “Sin datos registrados”; nunca diagnóstico confirmado ni negación clínica.
6. Dado campo inválido, cuando guarda, entonces recibe 400 con nombre/código seguro, sin valores ni modificación persistida.
7. Dado perfil ausente/incompleto/completo, cuando consulta completitud, entonces obtiene faltantes deterministas sin valores clínicos.
8. Dado fallo de red o 409, cuando termina la solicitud, entonces permanece borrador en memoria; al recargar tras confirmación o cerrar sesión, se descarta conforme al flujo.
9. Dado rol sin permiso o sesión inválida, cuando llama directamente, entonces es rechazado sin perfil ni metadatos.
10. Dada migración aplicada dos veces, cuando inicia la aplicación, entonces conserva perfiles/versiones y fechas históricas desconocidas; cliente anterior sigue funcionando.

## Dependencias, validación y aprobación

Propuestas para aprobación: obligatoriedad conservada, listas vacías como ausencia de registro, fechas por categoría con históricos null, endpoint de completitud y portal sin ampliación de permisos. La corrección formal de identidad y retención del contacto de emergencia siguen pendientes de política; no se crea trámite ni se promete canal automatizado.

Tras aprobación, preparar un patch revisable con OpenAPI, migración, API y Flutter en sus áreas. Validación futura: integración PostgreSQL, autorización, concurrencia, migración, auditoría, widgets y regresión de citas con datos sintéticos. No se ejecutaron pruebas ni se modificó código durante esta etapa.

## Handoff implementer

Backend: un responsable en api/src/. Flutter: ambos colaboradores coordinados en app/src/. Portal: sin implementación hasta definición posterior. Primero contrato backend revisado; después integración Flutter; Validator revisa patch y evidencia; integración por PR. Esta etapa termina en revisión humana obligatoria de H03-SPEC-v1 antes de invocar Implementer.
