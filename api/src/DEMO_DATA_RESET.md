# Dataset de demostración SEED-REALISTA-v2

Operación exclusivamente CLI de desarrollo, sin endpoint HTTP ni ejecución al iniciar la API.
El reemplazo borra los datos anteriores de las 17 tablas del manifiesto `telemedicina_m1`,
incluidos cuentas, auditoría, historial y sesiones. Conserva migraciones y `schema_versions`
(versiones 1–9), DDL y otros esquemas, incluido `public`. No se exige respaldo; un éxito no
se puede deshacer mediante este procedimiento. El rollback protege fallos anteriores al commit.

## Preparación y aprobación

Detener API y clientes. Compilar el parche revisado antes de usar los comandos. Configurar
privadamente `ASPNETCORE_ENVIRONMENT=Development`, `Milestone__Enabled=true`,
`Mail__Mode=development`, credenciales configuradas para Admin/Admission, `Milestone__DemoPassword`
(12–128 caracteres) y `ConnectionStrings__Telemedicina`.

No pasar contraseñas por argumentos, guardarlas en el repositorio ni imprimirlas. Los 30
correos se validan antes de abrir conexión. La previsualización read-only muestra el nombre
de la base, host, puerto y conteos actuales/previstos. Revisar ese informe y obtener aprobación
explícita antes de ejecutar `--execute`. La aprobación de la receta no sustituye la aprobación
del informe de conteos.

Desde la raíz del repositorio, mediante el mecanismo privado que carga la configuración:

```sh
dotnet run --project api/src/Telemedicina.Api -- --reset-demo-data
dotnet run --project api/src/Telemedicina.Api -- --reset-demo-data --base-date YYYY-MM-DD
dotnet run --project api/src/Telemedicina.Api -- --reset-demo-data --execute --base-date YYYY-MM-DD
```

Los dos primeros modos no cambian datos. Solo `--execute` confirma el reemplazo y requiere
aprobación posterior al informe. No mezclar con `--migrate`, otros seeds, `--check-smtp` u
otras opciones. Para un destino local, `DemoReset__ExpectedDatabase` debe coincidir con el
nombre de base revisado, y el host debe ser `localhost` o una IP loopback.

El único destino remoto permitido es
`pg-2db5ca83-telemedicina-iso910.l.aivencloud.com:10036/defaultdb`. Para ese destino,
configurar `DemoReset__ExpectedHost`, `DemoReset__ExpectedPort` y
`DemoReset__ExpectedDatabase` con esos valores exactos. Estas opciones solo confirman la
allowlist fijada en el código: cambiarlas no permite otros hosts, puertos ni bases. Si el
host, puerto, base o confirmación configurada no coincide, la ejecución aborta antes de abrir
la conexión. La revisión del informe y la aprobación posterior siguen siendo necesarias.
La fecha predeterminada se captura una vez en `America/Santo_Domingo`. Los
diez días laborables son posteriores a esa fecha; se rechaza un plan con slots ya pasados.
No se inventa calendario de feriados.

## Contenido previsto

- 30 usuarios activos y verificados: 24 Patient, 1 Administrator, 2 Admission,
  2 Practitioner y 1 Support.
- 24 perfiles adultos completos con apellidos `(DEMO)`, direcciones genéricas dominicanas
  y teléfonos ficticios. Sin cédulas, diagnósticos, licencias ni pólizas reales.
- 6 servicios sintéticos con marca DEMO y 6 profesionales de catálogo sin licencia ni
  vínculo inventado con las cuentas Practitioner.
- 800 slots futuros en 10 días laborables, 09:00–12:00 y 14:00–17:00 UTC-4; 60 bloqueados,
  sin solapamientos.
- **Cero citas, cero historial de citas y cero notificaciones.** El seed no inserta ni
  modifica citas. Las preferencias de email son `false`; sesiones, challenges, dev_mailbox,
  staff_invitations, idempotency y email_outbox quedan vacías.
- 1 evento `demo.reset_seed_applied` con versión y conteos, sin información personal.

Los IDs funcionales derivan de `SEED-REALISTA-v2` y serán distintos de v1. Admin y admisión
primaria conservan solo sus credenciales configuradas; los otros accesos usan la contraseña
demo privada. Los emails adicionales son exclusivamente ficticios (`example.invalid`).

## Protección

Preflight exige las 17 tablas más `schema_versions`, versiones 1–9, propietario con facultad
de ALTER, tres triggers esperados activos y ausencia de referencias externas que requieran
CASCADE. Rechaza tablas desconocidas y triggers alterados. La previsualización usa una
transacción read-only. La ejecución obtiene advisory locks en orden fijo, toma locks exclusivos
y repite preflight antes de escribir. Solo deshabilita los triggers de protección durante
TRUNCATE, sin CASCADE; los reactiva antes de insertar. Todo ocurre en una transacción.

El informe no contiene credenciales, correos, cadenas de conexión ni tokens. Incluye destino
saneado (base, host y puerto), fecha, dataset, estado y conteos antes/previstos/después. El
El operador debe detener el proceso si el destino informado no es el esperado antes de solicitar
autorización de ejecución.

## Verificación

- Compilar API .NET 10 y ejecutar la suite normal `Telemedicina.Tests`: incluye guardas,
  argumentos, credenciales/colisiones y plan determinista de usuarios, perfiles, agenda y
  conteos cero de citas, historial y notificaciones.
- La integración opt-in solo se ejecuta contra una BD aislada cuyo nombre comience por
  `telemedicina_demo_reset_test`; no migrar ni instalar herramientas automáticamente.
  Comprueba dry-run sin cambios, rollback, triggers, integridad y repetición.
- La migración 009 restaura únicamente los guards append-only faltantes. Aplicarla
  explícitamente en `Development` y verificar los tres triggers habilitados antes del
  preflight; nunca desactivar todas las protecciones.
- No ejecutar reset `--execute` hasta revisar el informe real y obtener autorización explícita.
- Conservar evidencia saneada de compilación, pruebas y conteos; no afirmar validaciones no
  ejecutadas.
