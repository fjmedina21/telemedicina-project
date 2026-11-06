# H06-SPEC-v1 — Base técnica condicionada para admisión virtual

**Estado:** aprobada para preparar un parche de implementación; activación clínica/legal excluida.  
**Áreas:** `api/src/`, `app/src/`.  
**Dependencias:** M01, M02, M04, M05.

## Alcance

Preparar la base técnica de admisión virtual manteniéndola deshabilitada hasta que existan configuraciones clínicas y legales aprobadas. No se incluyen preguntas ni respuestas de triaje, criterios de urgencia o exclusión, mensajes de derivación, texto ni efecto legal del consentimiento, autoridad de tutores, ventana de check-in, verificación clínica de identidad ni requisitos para habilitar video. No se capturan ni transmiten medios.

## Historias

| ID | Área | Resultado |
|---|---|---|
| H06-API-01 | API | Consultar requisitos de check-in con autorización de la cita y responder deshabilitado sin configuración aprobada. |
| H06-APP-01 | Flutter | Mostrar el estado deshabilitado sin ofrecer triaje, consentimiento, check-in ni video. |
| H06-QA-01 | API/Flutter | Verificar autorización, aislamiento, estado inactivo, cambio de sesión y privacidad con datos sintéticos. |

## Contrato API propuesto

`GET /api/v1/appointments/{id}/check-in-requirements`

Para una cita accesible cuando no hay configuración activa:

```json
{
  "data": {
    "status": "NotConfigured",
    "canProceed": false,
    "configurationVersion": null
  }
}
```

No se implementan endpoints que acepten respuestas de triaje, contenido o aceptación de consentimiento, ni una transición a `Ready`. La identidad y la cita se determinan en el servidor.

## Autorización

- Patient solo consulta sus propias citas; cita ajena devuelve `404`.
- Admission solo obtiene lectura dentro de sus permisos vigentes.
- Otros roles reciben `403`.
- Sesión ausente, revocada o expirada recibe `401`.

## Flujo Flutter

La app consulta el endpoint con la sesión vigente. Con `NotConfigured`, muestra un aviso técnico no clínico, por ejemplo: “La admisión virtual aún no está disponible”. No habilita acciones de admisión. Al cerrar sesión o cambiarla, elimina el estado cargado. La comprobación interactiva de cámara/micrófono queda fuera de esta versión.

## Privacidad

No se guardan respuestas clínicas ni aceptaciones de consentimiento. No se escriben datos sanitarios, texto clínico, tokens ni secretos en logs, URL, notificaciones, telemetría ni caché persistente. No se solicitan permisos de cámara o micrófono ni se captura o transmite audio/video.

## Criterios verificables

1. Con una cita propia y sin configuración, Patient recibe `NotConfigured`, `canProceed=false` y versión nula.
2. Patient no obtiene información de citas ajenas (`404`); una sesión inválida recibe `401`; un rol no autorizado recibe `403`.
3. Flutter no presenta cuestionario, consentimiento, check-in ni video en estado `NotConfigured`.
4. Respuestas tardías de una sesión anterior no reinstalan estado privado.
5. No existen campos ni endpoints que reciban respuestas clínicas o contenido/aceptación del consentimiento en este incremento.
6. Las pruebas usan datos sintéticos y cubren autorización, acceso horizontal, sesión y regresión M01/M02/M04/M05.

## Decisiones para una versión futura

Requieren aprobación de responsables clínicos: contenido, umbrales y derivaciones. Responsables legales: texto, alcance y representación. Producto: ventana operativa y condiciones de activación. Esta aprobación no autoriza esos contenidos ni activa el flujo clínico.

## Handoff

Backend define el DTO/OpenAPI antes de integrar Flutter. No se amplían permisos del portal. Rutas de implementación candidatas: `api/src/Telemedicina.Api/Features/CheckIn/` y `app/src/lib/features/check_in/`.
