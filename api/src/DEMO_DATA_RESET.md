# Dataset de demostración SEED-REALISTA-v1

Operación exclusivamente CLI de desarrollo, sin endpoint HTTP ni ejecución al iniciar la API.
El reemplazo borra los datos anteriores de las17 tablas del manifiesto de telemedicina_m1,
incluidos cuentas, auditoría, historial y sesiones. Conserva schema_versions, DDL y otros
esquemas, incluido public. No se exige respaldo; un éxito no se puede deshacer mediante
este procedimiento. El rollback protege fallos anteriores al commit.

## Preparación

Detener API y clientes. Compilar el parche revisado antes de usar los comandos.
Configurar privadamente:
- ASPNETCORE_ENVIRONMENT=Development y Milestone__Enabled=true.
- Mail__Mode=development; este reset no envía correo.
- Milestone__AdministratorEmail y Milestone__AdministratorPassword actuales.
- Milestone__AdmissionEmail y Milestone__AdmissionPassword actuales.
- Milestone__DemoPassword nuevo, de12–128 caracteres, solo en entorno privado.
- ConnectionStrings__Telemedicina del ambiente autorizado.

No pasar contraseñas por argumentos, guardarlas en el repositorio ni imprimirlas.
No es necesario modificar .env: las claves pueden proporcionarse temporalmente por el
mecanismo privado de ejecución. Los correos primarios no aparecen en informes.
Todos los30 correos se normalizan/validan sin colisiones antes de escribir.

Desde api/src, usando el mecanismo privado que cargue la configuración:

    dotnet run --project Telemedicina.Api -- --reset-demo-data
    dotnet run --project Telemedicina.Api -- --reset-demo-data --base-date YYYY-MM-DD
    dotnet run --project Telemedicina.Api -- --reset-demo-data --execute --base-date YYYY-MM-DD

El primer modo es previsualización sin cambios. Solo --execute confirma el reemplazo.
No mezclar con --migrate, otros seeds, --check-smtp ni otras opciones.
La fecha base predeterminada se captura una vez en America/Santo_Domingo.
Los diez días laborables son posteriores a esa base; se rechaza un plan con slots ya
pasados respecto al momento de ejecución. No se inventa calendario de feriados.

## Contenido

30 usuarios activos/verificados:24 Patient,1 Administrator,2 Admission,2 Practitioner,1 Support.
Admin y admisión primaria conservan únicamente sus credenciales configuradas; los IDs
se recrean de manera determinista. Los otros accesos usan DemoPassword privado.

Correos adicionales, exclusivamente ficticios:
- admision.auxiliar.demo@example.invalid
- profesional.uno.demo@example.invalid
- profesional.dos.demo@example.invalid
- soporte.demo@example.invalid
- paciente.01.demo@example.invalid hasta paciente.24.demo@example.invalid

24 perfiles adultos completos, apellidos con (DEMO), direcciones genéricas dominicanas y
teléfonos ficticios del rango555-01xx. Sin cédulas, diagnósticos, licencias ni pólizas reales.
Declaraciones de alergias/medicación/condiciones vacías; no se simula validación clínica.

6 servicios con marca DEMO y is_synthetic=true:
medicina general30min, familiar30min, seguimiento medicina interna30min,
orientación psicológica40min, nutricional20min y dermatológica20min.
6 profesionales de catálogo sin licencia, uno por servicio; no existe vínculo inventado
con las cuentas Practitioner. No se afirma habilitación clínica.

10 días laborables09–12 y14–17 UTC-4, slots completos sin solapamientos:
800slots/60bloqueados (uno por profesional/día).
30 citas en slots diferentes no bloqueados:
12 PendingValidation/Pending,8 Confirmed/Approved,6 Cancelled/Pending,
4 PendingValidation/Denied. Toda evidencia/aseguradora indica simulación.
30 solicitudes iniciales y18 transiciones producen48 historias mediante el trigger real.
48 notificaciones neutras.24 preferencias de email=false.
Vacíos: sesiones,challenges,dev_mailbox,staff_invitations,idempotency,email_outbox.
1 evento nuevo demo.reset_seed_applied con versión/conteos, sin información personal.

## Protección

Preflight exige versiones1–5 exactas, las17 tablas más schema_versions, propietario
con facultad de ALTER y los tres triggers esperados activos. Rechaza tablas desconocidas,
triggers alterados y referencias externas que requirieran CASCADE.
La previsualización usa una transacción read-only. La ejecución obtiene advisory locks
19041301/02/03/04 en ese orden, locks exclusivos fijos y repite preflight antes de escribir.
Solo se deshabilitan audit_no_mutation y history_no_mutation durante TRUNCATE de las17
tablas, con RESTART IDENTITY y sin CASCADE. Se reactivan inmediatamente antes de insertar.
El trigger de historial permanece habilitado. Todo, incluidos cambios de triggers, va en
una transacción: errores revierten estado y datos. No hay fallback destructivo.
El resumen contiene conteos antes/previstos/después, dataset, fecha y estado committed.
No contiene contraseñas, correos privados, cadenas de conexión ni tokens.

## Verificación pertinente

- Compilar API .NET10 y ejecutar Telemedicina.Tests normal: incluye guardas, argumentos,
  credenciales/colisiones y plan determinista con agenda, bloques y distribución.
- Integración opt-in en una BD de prueba separada YA migrada001–005. Su nombre debe
  empezar por telemedicina_demo_reset_test. Configurar privadamente
  DEMO_RESET_DB_TESTS=1 y DEMO_RESET_TEST_CONNECTION_STRING, luego ejecutar
  Telemedicina.Tests con --demo-reset-db-tests.
  Comprueba dryrun sin cambios, fallo después de truncate/rollback, triggers activos,
  integridad y repetición funcional. Esta prueba también reemplaza datos de esa BD aislada.
  No migrar ni instalar herramientas automáticamente.
- Smoke HTTP en ambiente autorizado antes del reset final: login privado Admin/Admission/
  paciente, perfil/citas propias, cola Admission, rechazo de Admin a pacientes y slots futuros.
  No ejecutar la suite histórica completa después del reset final: vuelve a crear datos.
- Terminar con el reset final y servicios detenidos; así no quedan sesiones ni nuevas colas.
- Conservar evidencia saneada de build/tests/conteos. Ninguna prueba está afirmada por
  este documento antes de su ejecución real.

IDs y datos funcionales se repiten con la misma fecha base. Hashes de contraseña y
timestamps técnicos pueden variar. No existe copia de seguridad automática.
