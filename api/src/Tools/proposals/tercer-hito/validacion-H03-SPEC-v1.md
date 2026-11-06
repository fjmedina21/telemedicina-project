# Validación local — H03-SPEC-v1

Fecha: 2026-11-05, America/Santo_Domingo.
Estado: parche aplicado y verificaciones locales PASS. Ejecución sin harness ni API externa de IA.

## Aprobación y revisión

El usuario aprobó explícitamente: «apruebo H03-SPEC-v1, continua sin el harnes».
Constancia con huella de la especificación: [aprobacion-H03-SPEC-v1.md](aprobacion-H03-SPEC-v1.md).
El orquestador coordinó Implementer backend, Implementer Flutter y Validator directamente.
Validator revisó los parches y las correcciones; veredicto técnico PASS, sin bloqueadores pendientes.
El orquestador ejecutó las verificaciones descritas abajo; el validador no afirmó ejecutar pruebas.

## Cambios aplicados

- API: normalización y validación segura por campo, rechazo de identidad/metadatos editables, completitud, metadatos de fuente/fecha por categoría, versión y auditoría transaccionales.
- Migración 006 explícita: tres columnas timestamptz nullable; conserva fechas históricas desconocidas.
- OpenAPI: respuestas tipadas y nuevo endpoint GET /api/v1/patients/me/completeness.
- Flutter: repositorio/DTO del perfil, listas por elemento que conservan comas, fuente/fecha/completitud, borrador conservado ante errores/refresh/navegación, conflicto y recarga confirmada, limpieza de datos privados.
- Corrección del fixture de regresión: elige una asociación activa real profesional/servicio; conserva las aserciones y no modifica catálogo ni código de negocio de citas.

## Verificaciones ejecutadas

| Verificación | Comando o suite | Resultado real |
|---|---|---|
| Compilación | dotnet build api/src/Telemedicina.sln --configuration Release --no-restore | PASS, cero errores |
| Contratos .NET | dotnet run --no-build --configuration Release --project api/src/Telemedicina.Tests/Telemedicina.Tests.csproj | 25/25 PASS |
| Flutter | flutter test --no-pub --reporter expanded, en app/src | 25/25 PASS |
| Análisis Flutter | flutter analyze --no-pub --no-fatal-infos, en app/src | Exit 0; cero errores y advertencias; seis avisos informativos de estilo |
| Migración real | --migrate con API Release y configuración privada Development cargada en memoria | H03_ADDITIVE_MIGRATION: PASS |
| PostgreSQL real | python3 api/src/Tools/run_h03_checks.py | H03_DB_CHECKS_PASS |
| HTTP/OpenAPI reales | python3 api/src/Telemedicina.Tests/Integration/third_milestone_e2e.py -v | 1 recorrido compuesto PASS, 105.532 s |
| Regresión citas HTTP | milestone_e2e.py -v MilestoneE2E.test_02_idempotency_conflicts_and_database_read MilestoneE2E.test_05_blocked_slot_and_profile_concurrency | 2/2 PASS, 29.397 s |
| Integridad diff | git diff --check | PASS |
| Correspondencia de parches | git apply --reverse --check, por cada parche guardado | PASS; no revierte archivos |

Las pruebas PostgreSQL verificaron creación, metadatos, conservación de históricos, migración repetida dos veces, vaciado de categoría, identidad, aislamiento, concurrencia, auditoría y revocación. Se creó únicamente información sintética de prueba.

La suite HTTP verificó autenticación y autorización Patient/Admission, validación de tipos y errores seguros, identidad protegida, aislamiento, metadatos sin cambios espurios, concurrencia y esquemas OpenAPI.
Las pruebas de Flutter incluyen repositorio, controlador y widgets; sus transportes HTTP son simulados. No equivalen a una corrida móvil completa contra la API real.

Las API de prueba escucharon en puertos loopback temporales y se detuvieron al finalizar. Configuración privada solo en memoria; correo forzado a development; sin correo real ni cuerpos sensibles en la salida.

## Errores encontrados y corregidos

1. Literal JSON mal escapado en una prueba .NET: corregido con literal raw, compilación y suite PASS.
2. Promoción de nulidad en el repositorio Flutter: corregida comprobación explícita data != null, compilación y suite PASS.
3. Recarga que podía restaurar valores anteriores: retirado reset obsoleto; prueba verifica revisión nueva con contenido distinto.
4. Fecha de nacimiento comparada según zona local: comparación con componentes UTC.
5. Fixture antiguo escogía servicio y profesional sin comprobar asociación: primera regresión 2/2 falló con HTTP 400; selección de pareja activa corregida y repetición 2/2 PASS.

Se respetó la instrucción posterior «solo corrigue sin son errores»: no se corrigieron los seis avisos informativos de estilo restantes. La advertencia CS8602 de DemoResetDbChecks.cs:18 ya existía en la compilación inicial y permanece sin cambios.

## Parches y límites

- [Parche backend](h03-backend.patch): 12 archivos, incluye la corrección del fixture de regresión.
- [Parche Flutter](../../../../../app/src/Tools/proposals/tercer-hito/h03-flutter.patch): 10 archivos.
- Huellas y comprobación de correspondencia: [verificacion-parches.json](verificacion-parches.json).

Cambios locales limitados a api/src y app/src. Portal y dependientes permanecen fuera del alcance H03 aprobado.
No se realizó commit, integración remota ni despliegue. La revisión por PR y la revisión humana del equipo permanecen como etapas posteriores; las verificaciones locales no autorizan producción.
