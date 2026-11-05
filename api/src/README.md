# API de Telemedicina

ASP.NET Core **net10.0**, PostgreSQL/Npgsql y MailKit.
SDK validado: 10.0.403; runtime validado: 10.0.14. No requiere roll-forward a otra versión mayor.

## Organización

- Features/Identity: acceso, verificación, recuperación, sesiones y administración de cuentas.
- Features/Patients: perfil y datos autodeclarados.
- Features/Catalog: catálogo público, servicios y profesionales.
- Features/Appointments: reservas, historial, cancelación y reprogramación.
- Features/Admission: cola y revisión manual de cobertura.
- Features/Scheduling: disponibilidad y gestión de horarios.
- Features/Notifications: avisos, preferencias y seguimiento de entregas.
- Features/Audit y Features/Health: trazabilidad y estado.
- Infrastructure/: autenticación, auditoría, correo y persistencia.
- Infrastructure/Persistence/Migrations/: migraciones versionadas.
- Shared/: respuestas de controladores y contratos de concurrencia.
- Configuration/: composición de servicios y middleware.
- Tools/: arranque privado y exportación de parches.

## Ejecución

Desde la raíz, con api/src/.env configurado de forma privada:

```bash
dotnet build api/src/Telemedicina.sln --configuration Release
python3 api/src/Tools/run_local.py --configuration Release --no-build -- --inspect
python3 api/src/Tools/run_local.py --configuration Release --no-build -- --migrate
python3 api/src/Tools/run_local.py --configuration Release --no-build
```

La migración es explícita, transaccional e idempotente. Usa telemedicina_m1 y conserva public.
La cuenta sintética de admisión se provisiona por --seed-synthetic con sus variables privadas.
Las rutas del hito requieren Development y Milestone__Enabled=true.

- Estado: http://localhost:5000/api/v1/health
- Swagger: http://localhost:5000/swagger
- OpenAPI: http://localhost:5000/openapi/v1.json

Mail__Mode=smtp utiliza el proveedor configurado; Gmail local usa STARTTLS en smtp.gmail.com:587.
Mail__Mode=development captura las entregas y habilita el buzón de desarrollo
protegido; no se usan las credenciales SMTP para enviar correo real.

## Pruebas

```bash
dotnet run --no-build --configuration Release --project api/src/Telemedicina.Tests/Telemedicina.Tests.csproj
```

La batería completa está en Telemedicina.Tests/Tools/run_milestone_checks.py; necesita variables
de conexión privadas en su proceso, una base sintética, dependencias del portal y Chromium de Playwright.
No carga automáticamente secretos de archivos ni usa Gmail; genera credenciales sintéticas y SMTP local.

Contratos y configuración: [PRIMER_HITO.md](PRIMER_HITO.md).
Resultados: [validación del primer hito](../../hitos/api/primer-hito-api/validaciones/01-base_completado.md).
