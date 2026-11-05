# Segundo hito Portal Web — alcance de trabajo

Plan coordinado con [el plan transversal de API](../../api/segundo-hito-api/PLAN.md). Estado: propuesta, no implementación.

## Objetivo

Ofrecer a los operadores autorizados una interfaz mínima para el ciclo de vida de cuentas de personal y hacer visibles los límites de acceso definidos por la API.

## Incluido

- Pantallas protegidas para listar/buscar cuentas de personal con los campos mínimos aprobados.
- Acciones de invitación/alta, activación, suspensión y revocación únicamente si la matriz aprobada las concede al usuario autenticado.
- Mostrar estado, rol y ámbito organizacional sin permitir autoconcederse permisos.
- Manejar errores de autorización, sesión revocada y conflictos de actualización.
- Confirmar acciones administrativas y reflejar auditoría disponible al operador.
- Mantener datos sintéticos en desarrollo; no guardar credenciales ni artefactos de autenticación en evidencias.
- Pruebas de navegador con Playwright para acceso permitido/denegado y ciclo de vida acordado.

## Fuera de alcance

Acceso a notas clínicas, documentos, video o expedientes; gestión de citas/agenda; soporte clínico; asignación de tutores; SSO o federación de identidad; producción.

## Criterios de aceptación

1. Rutas y acciones de administración requieren sesión y autorización del servidor.
2. Operadores sin permisos no pueden acceder a la pantalla ni ejecutar las operaciones mediante API.
3. La UI no expone secretos, tokens, códigos de verificación ni datos clínicos.
4. Estados de carga, lista vacía, error, conflicto, éxito y sesión vencida tienen respuesta clara.
5. Cada acción reflejada como exitosa procede de una respuesta confirmada por API y es auditable.
6. Los escenarios de navegador usan Playwright y datos sintéticos; se guardan solo capturas/resultados saneados.
7. Integración por PR después de revisar OpenAPI y la matriz de permisos.

## Handoff y dependencia

Depende de la matriz de autorización y contratos API aprobados. Responsable: área portal `portalweb/src/`. No se implementa ningún permiso clínico por conveniencia de interfaz.
