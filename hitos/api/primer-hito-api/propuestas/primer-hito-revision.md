# Estado actualizado

Parche aprobado por el usuario e implementado localmente.
Resultados actuales y límites: [validación base](../validaciones/01-base_completado.md) y [código de seis caracteres](../validaciones/02-verificacion-seis-caracteres_completado.md).

El texto siguiente se conserva como registro histórico de la propuesta previa.

---

# Primer hito: propuesta para revisión

No integrado. No se modificaron fuentes ni PostgreSQL.

Validación ejecutada: git apply --check --whitespace=error (correcto); sintaxis Python de pruebas (correcta).
Pendiente: inspección del esquema remoto, aprobación, restauración, compilación, análisis Flutter, pruebas E2E y UI.

El parche incorpora PRIMER_HITO.md con contrato, configuración privada, recorrido de aceptación, supuestos y limitaciones.
Entorno sintético local; proveedor de identidad/correo real y políticas clínicas siguen pendientes.
No se incluyeron credenciales en los artefactos. No declarar el hito completo antes de integrar y probar.

Archivos propuestos:

- api/src/PRIMER_HITO.md
- api/src/Telemedicina.Api/Milestone/001_initial.sql
- api/src/Telemedicina.Api/Milestone/AuthController.cs
- api/src/Telemedicina.Api/Milestone/Bootstrap.cs
- api/src/Telemedicina.Api/Milestone/CareController.cs
- api/src/Telemedicina.Api/Milestone/Contracts.cs
- api/src/Telemedicina.Api/Milestone/Database.cs
- api/src/Telemedicina.Api/Milestone/Security.cs
- api/src/Telemedicina.Api/Program.cs
- api/src/Telemedicina.Api/Telemedicina.Api.csproj
- api/src/Telemedicina.Tests/milestone_e2e.py
- app/src/android/app/src/main/AndroidManifest.xml
- app/src/lib/app/routes.dart
- app/src/lib/core/network/milestone_repository.dart
- app/src/lib/features/auth/presentation/login_screen.dart
- app/src/lib/features/auth/presentation/welcome_screen.dart
- app/src/lib/features/home/presentation/home_screen.dart
- app/src/lib/features/milestone/milestone_screen.dart
- app/src/lib/features/video_visit/presentation/video_visit_screen.dart
- app/src/lib/main.dart
- app/src/macos/Runner/DebugProfile.entitlements
- app/src/macos/Runner/Release.entitlements
- app/src/pubspec.yaml
- app/src/test/milestone_repository_test.dart
- app/src/test/widget_test.dart
- portalweb/src/next.config.ts
- portalweb/src/src/app/admin/page.tsx
- portalweb/src/src/app/admision/page.tsx
- portalweb/src/src/app/agenda/page.tsx
- portalweb/src/src/app/consulta/[id]/page.tsx
- portalweb/src/src/app/documentos/page.tsx
- portalweb/src/src/app/layout.tsx
- portalweb/src/src/app/login/page.tsx
- portalweb/src/src/app/page.tsx
- portalweb/src/src/components/admission-console.tsx
- portalweb/src/src/lib/milestone-api.ts
