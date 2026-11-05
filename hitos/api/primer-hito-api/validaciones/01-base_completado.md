# Primer hito API — implementación y validación

Estado: parche revisado y aprobado por el usuario, aplicado localmente.
Resultado: PASS para el incremento de desarrollo con datos sintéticos.
No implica cumplimiento completo de M01–M05 ni autorización clínica o de producción.

## Implementado

- PostgreSQL/Npgsql con conexión privada configurada en el proceso, sin credenciales en fuentes.
- Inspección previa de metadatos; no había tablas de aplicación visibles.
- Migraciones 001/002 en telemedicina_m1 y extensión btree_gist. Reejecución idempotente comprobada.
- Registro/verificación y recuperación mediante buzón de desarrollo protegido.
- Login de paciente y admisión; admisión requiere TOTP.
- Sesiones persistentes y revocables, rotación de refresh y familias para logout concurrente.
- Perfil autodeclarado con control de versiones.
- Catálogo, disponibilidad, creación/bloqueo de horarios.
- Reserva transaccional sin solapamientos; idempotencia por usuario y solicitud.
- Cancelación/reprogramación, revisión manual de cobertura con evidencia y vigencia.
- Historial resistente a modificaciones y consulta paginada de auditoría.
- Rutas del hito desactivadas fuera de Development o sin habilitación explícita.

## Evidencia ejecutada

| Verificación | Resultado |
|---|---|
| dotnet build Telemedicina.Api | PASS: 0 errores, 0 advertencias |
| Pruebas existentes de contratos .NET | PASS: 5/5 |
| Suite HTTP real milestone_e2e.py | PASS: 10/10, última ejecución 73.508 s |
| Persistencia tras reiniciar un proceso real | PASS dentro de la suite HTTP |
| Aplicación/reaplicación de migraciones | PASS |
| Salud/Swagger sin configuración del hito | HTTP 200 / 200 |
| Rutas del hito desactivado | HTTP 404 |
| Acceso del portal sin sesión | HTTP 401 |
| Flujo de revisión desde Chromium/Playwright | PASS; ver informe del portal |

Las pruebas HTTP cubren acceso cruzado, autorización por rol, idempotencia, reserva concurrente,
historial y versiones, revisión/reprogramación/cancelación, horarios bloqueados, refresh,
recuperación de contraseña, MFA negativa, reinicio real y revocación.

Se usó temporalmente DOTNET_ROLL_FORWARD=Major con runtime .NET 10 instalado; el target sigue net9.0.
Las pruebas generaron registros exclusivamente sintéticos. Las credenciales de conexión y de las
cuentas de prueba permanecieron en memoria; no se añadieron al repositorio ni a evidencias.
Se detuvieron las instancias temporales de pruebas.

## Ajustes posteriores al parche aprobado

- Corrección de la sobrecarga JSON en el handler de autenticación.
- Resolución diferida de la conexión: salud/Swagger no requieren configurar el hito.
- Validación de Origin sin excepciones por URI inválida.
- Migración 002 para preservar revocación tras rotación concurrente.
- Pruebas adicionales de seguridad y reinicio; documentación actualizada.
- Correcciones de análisis en pantallas Flutter antiguas.
- Playwright y corrección transitiva de PostCSS en el portal.

El parche original se conserva como evidencia de lo revisado; las fuentes contienen estos ajustes.
No se creó commit ni se fusionó una PR.

## Pendientes para cerrar el hito del plan y las specs

1. Proveedor real de correo/identidad: verificación, recuperación y operación de cuentas de personal.
2. Catálogo/profesionales habilitados y gestión administrativa; reglas de agenda, bloqueos y feriados.
3. Políticas aprobadas de anticipación, cancelación, reprogramación y atención particular/cobertura.
4. Confirmaciones y avisos mediante notificaciones/outbox, con deduplicación y reintentos.
5. CI completa: portal, pruebas HTTP/Playwright, ejecución correcta del runner .NET y runtime alineado.
6. Validación de Flutter en Android/iOS, además del macOS compilado.
7. Revisión de PR/UAT del recorrido completo por los responsables.
8. Antes de datos reales: rol PostgreSQL de mínimo privilegio, TLS con validación de certificado,
   retención y controles de auditoría/operación adecuados.

Configuración y contratos: ../../../api/src/PRIMER_HITO.md.
