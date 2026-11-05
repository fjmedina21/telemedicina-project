# Spec ejecutable SEED-REALISTA-v1

Estado original: propuesta para aprobación humana; sin implementación ni cambios de datos.
Basada en el diseño del Architect y contrastada con CLI, migraciones001–005 y seeders actuales.
La constancia separada documenta la aprobación posterior del usuario.

Área: backend api/src/. Responsable: backend_mobile. Contratos HTTP, Flutter y portal sin modificaciones.

## Objetivo y alcance

Reemplazar los datos existentes de desarrollo por un conjunto sintético coherente, útil para
recorrer catálogo, perfil, citas y administración de personal.
La operación borra también auditoría, historial, sesiones y cuentas actuales del esquema
telemedicina_m1. Conserva schema_versions, tablas, restricciones, índices, funciones,
extensiones y cualquier otro esquema, incluido public.

No se exige respaldo en esta versión. Un reset completado no podrá deshacerse con este
procedimiento. La transacción permite recuperar los datos anteriores si la operación falla
antes del commit; no sustituye un respaldo.

## Historias y operación

SEED-01: previsualizar tablas afectadas y conteos sin modificar datos.
SEED-02: ejecutar explícitamente el reemplazo completo por el dataset versionado.
SEED-03: obtener resumen saneado y verificaciones de integridad sin contraseñas ni conexión.

Nuevo comando --reset-demo-data, previsualización predeterminada; ejecución solo --execute.
Fecha base opcional --base-date YYYY-MM-DD; si falta capturar una única fecha local de
Santo Domingo. No combinar con migraciones, seeds anteriores ni otros comandos operativos.

Precondiciones:
1. Development, Milestone:Enabled=true, Mail:Mode=development.
2. API y clientes detenidos.
3. Credenciales privadas válidas, correos normalizados sin colisiones, DemoPassword12–128.
4. Versiones de esquema exactamente1–5 y manifiesto conocido17 tablas más schema_versions.
   Las tablas desconocidas bloquean la operación.
5. Validación completa antes del primer cambio.

Manifiesto reemplazado:
users,profiles,sessions,challenges,dev_mailbox,services,practitioners,slots,appointments,
appointment_history,idempotency,audit,practitioner_services,notification_preferences,
notifications,email_outbox,staff_invitations.

Nombres plenamente cualificados, una transacción, locks exclusivos y advisory locks de
migración/seeds en orden fijo. Únicamente durante truncate se deshabilitan triggers
audit_no_mutation e history_no_mutation; reactivar inmediatamente antes de insertar.
No CASCADE, session_replication_role ni deshabilitación global.

## Dataset aprobado por esta versión

24 pacientes adultos ficticios con perfiles completos y marca visible de demostración.
6 personales activos:1 Administrator,2 Admission,2 Practitioner,1 Support.
30 usuarios totales verificados, sin sesiones precreadas.
6 servicios sintéticos/6 profesionales con vinculaciones explícitas.
Agenda:10 días laborables futuros,09–12 y14–17 America/Santo_Domingo.
30 citas:12 pendientes,8 confirmadas,6 canceladas,4 pendientes con cobertura denegada.
Email deshabilitado para24 pacientes.
Outbox,sesiones,challenges,mailbox,invitaciones,idempotencia vacíos.

Servicios demo:medicina general30min, familiar30min, seguimiento medicina interna30min,
orientación psicológica40min, nutricional20min, dermatológica20min.
Todos is_synthetic=true sin referencias que aparenten aprobación clínica.
Un profesional por servicio, sin licencia real y sin inventar FK con usuarios Practitioner.

Únicamente slots completos dentro de cada jornada:800slots,60bloqueados (uno por
profesional/día). Las30 citas usan slots distintos no bloqueados. Fecha base fija reproduce
fechas/identificadores de negocio; hashes y timestamps técnicos pueden variar.

Perfiles con nombres plausibles, cumpleaños adultos, direcciones genéricas de Santo Domingo,
Santiago,La Vega,San Cristóbal, correos example.invalid y contactos ficticios.
No cédulas,licencias,pólizas,recetas ni diagnósticos reales.

Todas las citas nacen PendingValidation/Pending. Las transiciones generan historial mediante
el trigger existente:8 Confirmed/Approved,6 Cancelled/Pending,4 PendingValidation/Denied.
Evidencias/aseguradoras indican demostración sin validación ARS.
Sin estados no implementados Completed/NoShow.
Notificaciones neutras coherentes con solicitudes/transiciones, sin envíos externos.
Auditoría nueva identifica dataset/versión/resultado sin información personal/secretos.

Administrador y admisión principal reutilizan credenciales privadas configuradas; los demás
accesos emplean DemoPassword privado. Documentar solo correos sintéticos y claves de
configuración, nunca valores secretos.

## Criterios de aceptación

1. Guardas:entorno,correo,credenciales/esquema inválidos fallan antes de modificar datos.
2. Previsualización:mismos conteos/contenido antes/después; alcance y conteos previstos.
3. Rollback:fallo de prueba después de truncate restaura datos previos y triggers activos.
4. Integridad:30usuarios/24perfiles/6servicios/6profesionales/800slots/60bloqueados/30citas,
   distribución definida, sin referencias rotas ni solapamientos.
5. Historial:30solicitudes+18transiciones=48entradas; protección DELETE/TRUNCATE restaurada.
6. Aislamiento:schema_versions y objetos externos al manifiesto permanecen iguales.
7. Repetición:dos resets con fecha fija conservan contenido funcional/conteos sin acumular.
8. Build .NET y pruebas relevantes aprobadas con evidencia saneada.
9. Smoke HTTP:login privado Admin/Admission/Patient, perfil/citas propias, cola Admission,
   Admin sin acceso a pacientes y disponibilidad futura. Antes del reset final para dejar
   sesiones/colas vacías.
10. Entrega:resumen final de conteos/integridad y servicios detenidos.

## Puerta humana

Aprobar explícitamente SEED-REALISTA-v1, incluido borrado de auditoría/historial de desarrollo
y ausencia de respaldo obligatorio. Después Implementer propone parche único, Validator
revisa y humano revisa antes de aplicarlo. Todo el dataset es simulación, sin decisión clínica.
