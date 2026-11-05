# Primer hito Flutter — validación

Estado: implementación local integrada para datos sintéticos.

## Funcionalidad

Acceso/registro/verificación/recuperación, perfil autodeclarado, catálogo y disponibilidad,
solicitud de cita, consulta de estados, cancelación y reprogramación contra API real.
Tokens únicamente en memoria. La app ya no arranca en el contador de ejemplo.

## Verificaciones ejecutadas

- flutter pub get: PASS; pubspec.lock actualizado.
- flutter analyze: PASS, sin hallazgos.
- flutter test: PASS, 4/4.
- flutter build macos --debug: PASS.

Las pruebas cubren arranque de acceso, preservación de 409, renovación de sesión con la misma
Idempotency-Key y limpieza de tokens aunque falle el logout por red.
Se corrigieron errores de compilación y análisis en prototipos antiguos, sin habilitarlos en la navegación.

## Pendientes

Validar en Android/iOS reales o simuladores; completar correo/identidad real con el backend;
aprobar reglas del servicio y ejecutar UAT del paciente. Este informe no afirma que se realizaron
pruebas de dispositivos móviles ni una sesión clínica.

Las pruebas web que requieren navegador usan Playwright; evidencias en
../../../portalweb/primer-hito-portalweb/validaciones/01-base_completado.md.
