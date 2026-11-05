# App Flutter — primer hito

La app está organizada por responsabilidad:

- lib/app/: composición, navegación del paciente y estado de aplicación.
- lib/features/auth/: acceso y recuperación, con su repositorio de datos.
- lib/features/patient_profile/: formulario del perfil.
- lib/features/catalog/: disponibilidad y solicitud de citas.
- lib/features/appointments/: listado, cancelación y reprogramación.
- lib/features/notifications/: avisos y preferencias.
- lib/core/: red, errores y modelos compartidos.
- lib/shared/: formato, acciones y widgets reutilizables.
- lib/prototypes/: pantallas antiguas, fuera del flujo activo.
- test/app, test/core y test/features/: pruebas por responsabilidad.

Desde app/src:

```bash
flutter pub get
flutter analyze
flutter test
flutter run -d macos --dart-define=API_BASE_URL=http://127.0.0.1:5000/api/v1
```

Configurar primero la API según [su guía](../../api/src/README.md).
El cliente HTTP conserva tokens solo en memoria y preserva la clave de idempotencia al reintentar.
El perfil muestra sus datos como autodeclarados. Los avisos de cita y sus preferencias usan la API real.
El registro solicita el código de seis letras o números recibido por correo. La app lo verifica junto al correo del paciente.
El proveedor de correo depende de la configuración del backend; las pruebas usan SMTP local.

La refactorización se verifica con pruebas unitarias/widgets y análisis estático.
La validación Android/iOS fue retirada de los pendientes por instrucción del usuario.
Resultados: [validación del primer hito](../../hitos/app/primer-hito-app/validaciones/01-base_completado.md).
