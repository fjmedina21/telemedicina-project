# Implementación propuesta SEED-REALISTA-v1

Estado: propuesta; no aplicada ni ejecutada contra BD. Área exclusiva api/src/.
Constancia de aprobación en aprobacion-humana.md; spec en SPEC.md.

Incluye plan determinista, comando --reset-demo-data (dryrun por defecto), preflight estricto,
locks19041301/02/03/04, rollback transaccional y dataset aprobado.
No modifica API HTTP, clientes, migraciones históricas, .env, public ni schema_versions.
No crea backup: un éxito de reset no se deshace con este procedimiento.
La contraseña demo nueva se proporciona temporalmente en entorno privado, sin modificar.env.

Configuración previa:Development,Milestone enabled,Mail development, credenciales actuales
Admin/Admission y DemoPassword privado válido sin correos duplicados.
El preflight comprueba manifiesto18tablas totales, versiones1–5, ownership heredado/superuser,
FK externas y tres triggers exactos activos.
Dryrun read-only; execute bloquea todas las tablas conocidas, repite preflight, deshabilita
solo dos protectores para truncate17sinCASCADE/RESTARTIDENTITY y los restaura antes seed.
Los30 usuarios se insertan Admin primero, creador FK válido, failed_logins/locked_until0.
Perfiles camelCase compatibles.30 citas/48 historias y48 notificaciones sin email/outbox.
Sesiones y secretos temporales vacíos. Toda agenda future UTC-4 y no bloqueados reservados.

Pruebas propuestas:
- Unitarias:opciones,guardas entorno/correo,credenciales/colisiones,plan/IDs,agenda,
  bloques y distribución; incluidas en ejecutable Telemedicina.Tests.
- DB opt-in independiente:requiere nombre de BD telemedicina_demo_reset_test*, esquema ya
  migrado, DEMO_RESET_DB_TESTS=1 y conexión privada exclusiva. Prueba dryrun sin cambios,
  rollback tras truncate, protecciones, integridad y repetición. No se ejecuta por defecto.
- Smoke HTTP antes de reset final y servicios detenidos al terminar (coordinación posterior).

Verificación del parche:verificacion-parche.json.
No se afirma compilación, pruebas funcionales, migración ni reset en este turno.

Handoff Validator:
Revisar orden de locks, manifiesto/privilegios/triggers, límites de borrado, inserciones/FK,
compatibilidad de perfiles, generación800/60 y30/48, neutralidad notificaciones,
ausencia de secretos y rechazo de comandos combinados. Confirmar guardas y rollback
mediante ejecución real solo después de aprobación del parche.
