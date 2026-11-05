# Primer hito portal — validación con Playwright

Estado: implementación local integrada y verificada en Chromium real con datos sintéticos.

## Resultados

- npm run build: PASS.
- Playwright Test / Chromium: PASS, 5/5; última ejecución 24.2 s.
- npm ci --ignore-scripts: PASS.
- npm audit: 0 vulnerabilidades tras corregir PostCSS transitivo de Next.js a 8.5.32.
- Proxy conectado a la API real y PostgreSQL; acceso sin sesión devuelve 401.
- Capturas de admisión y horarios revisadas visualmente.

## Pruebas de navegador

1. Navegación no autenticada redirige al login.
2. Login real con contraseña y TOTP; cookies HttpOnly y SameSite=Strict;
   respuesta del navegador sin accessToken ni refreshToken.
3. Paciente sintético crea solicitud; admisión revisa desde el navegador;
   Confirmed/Approved se verifica en PostgreSQL a través de la API.
4. Navegación por auditoría y horarios con datos del servidor;
   bloqueo de horario y restauración del mismo tras comprobar la acción.
5. Logout revoca sesión, elimina cookies y vuelve a proteger la navegación.

Se corrigió el espaciado de la cola a partir de las capturas y se repitió la suite: 5/5.
No se usaron datos reales de pacientes. No se guardaron credenciales ni storageState;
trazas, vídeos y capturas automáticas de login permanecen desactivados.

## Evidencias

- [Resultados JSON](evidencias/playwright-results.json)
- [Admisión confirmada](evidencias/admision-confirmada.png)
- [Auditoría](evidencias/auditoria.png)
- [Horarios](evidencias/horarios.png)
- [Auditoría npm](evidencias/npm-audit.json)

Código y ejecución reproducible: ../../../portalweb/src/README.md.
La API y el portal deben ejecutarse con el mismo entorno sintético y las variables privadas
necesarias; ejecutar npm run test:e2e desde portalweb/src con MILESTONE_TEST_SYNTHETIC=1.

## Límites

Probado en Chromium de escritorio. No se afirma validación en Firefox/WebKit o navegadores móviles.
Identidad/correo de producción, reglas operativas, notificaciones, CI y UAT siguen pendientes.
Video, atención clínica y documentos no están habilitados.
Los servidores de validación se detuvieron al terminar.
