# Portal de admisión — primer hito

Next.js 15 / React 19 / TypeScript, conectado a la API .NET 10.
Las páginas de src/app/ son entradas del router; las funcionalidades están en src/features/.

| Carpeta | Responsabilidad |
|---|---|
| features/auth | Acceso de admisión |
| features/admission | Cola y revisión manual |
| features/catalog | Servicios, profesionales y asignaciones |
| features/scheduling | Gestión de horarios |
| features/audit | Auditoría paginada |
| features/notifications | Seguimiento de entregas |
| shared/api, shared/formatting, shared/components | Cliente HTTP, fechas y composición del portal |
| prototypes | Datos/vistas de referencia sin uso en el flujo activo |
| tests/browser/first-milestone | Flujo serial del hito con Chromium |
| tests/browser/support | Soporte de pruebas del navegador |

Rutas: /login, /admision, /catalogo, /agenda, /admin y /entregas.
La consulta clínica devuelve 404 y los documentos no están habilitados.

Desde portalweb/src:

```bash
npm ci --ignore-scripts
npm run build
npm run dev
```

El proxy /api/backend usa API_INTERNAL_URL (servidor), por defecto http://127.0.0.1:5000/api/v1.
No introducir secretos en variables NEXT_PUBLIC. Sesión con cookies HttpOnly/SameSite=Strict,
sin tokens en localStorage. La API valida el rol y el origen de las solicitudes.

## Playwright

Con API y portal iniciados en el mismo entorno sintético y las variables privadas de pruebas configuradas:

```bash
npx playwright install chromium
MILESTONE_TEST_SYNTHETIC=1 npm run test:e2e
```

La batería completa puede iniciarse con api/src/Telemedicina.Tests/Tools/run_milestone_checks.py.
Incluye login con MFA, revisión persistente, auditoría, horarios, catálogo, entregas y logout.
Traces, vídeos y capturas de login están desactivados para no registrar secretos.

Evidencias: hitos/portalweb/primer-hito-portalweb/evidencias/playwright/.
Auditoría de dependencias: evidencias/dependencias/.
Resultados actuales: [validación del primer hito](../../hitos/portalweb/primer-hito-portalweb/validaciones/01-base_completado.md).
