# App Flutter para Android e iOS

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
flutter devices
flutter run -d <id-del-simulador-o-iphone> --dart-define=API_BASE_URL=http://127.0.0.1:5000/api/v1
```

Para iPhone se requiere Xcode y un simulador iOS o un dispositivo conectado. Abre Simulator, lista los destinos con `flutter devices` y usa el identificador del simulador. En un iPhone físico, configura `API_BASE_URL` con la IP de red de la máquina que ejecuta la API; `127.0.0.1` apunta al propio iPhone. En el emulador Android, usa `http://10.0.2.2:5000/api/v1`.

Configurar primero la API según [su guía](../../api/src/README.md).
El cliente HTTP conserva tokens solo en memoria y preserva la clave de idempotencia al reintentar.
El perfil muestra sus datos como autodeclarados. Los avisos de cita y sus preferencias usan la API real.
El registro solicita el código de seis letras o números recibido por correo. La app lo verifica junto al correo del paciente.
El proveedor de correo depende de la configuración del backend; las pruebas usan SMTP local.

La app contiene únicamente los scaffolds móviles Android e iOS. La compilación y ejecución iOS requieren Xcode.
Resultados: [validación del primer hito](../../hitos/app/primer-hito-app/validaciones/01-base_completado.md).
