# Hitos del proyecto

La hoja de ruta única define los doce hitos, alcance, dependencias y estado: [ROADMAP.md](ROADMAP.md).

Cada hito trabajado se organiza en carpetas por componente. Los planes y validaciones se mantienen en sus áreas:

- API: `hitos/api/<hito-api>/`
- App: `hitos/app/<hito-app>/`
- Portal Web: `hitos/portalweb/<hito-portalweb>/`

## Hitos actuales

### Hito 01 — Base usable

Estado: completado para el alcance usable con datos sintéticos; no implica producción. Los informes de cierre usan el sufijo `_completado`:

- [API — base](api/primer-hito-api/validaciones/01-base_completado.md)
- [API — código de verificación](api/primer-hito-api/validaciones/02-verificacion-seis-caracteres_completado.md)
- [App — base](app/primer-hito-app/validaciones/01-base_completado.md)
- [App — identidad visual](app/primer-hito-app/validaciones/03-identidad-visual_completado.md)
- [Portal — base](portalweb/primer-hito-portalweb/validaciones/01-base_completado.md)
- [Portal — identidad visual](portalweb/primer-hito-portalweb/validaciones/03-identidad-visual_completado.md)

### Hito 02 — Roles y operación de cuentas

Estado: **completado** en el alcance aprobado y verificado localmente. PR, UAT y go/no-go de piloto/producción son etapas posteriores:

- [API](api/segundo-hito-api/PLAN.md)
- [App](app/segundo-hito-app/PLAN.md)
- [Portal Web](portalweb/segundo-hito-portalweb/PLAN.md)

### Hito 03 — Perfil propio del paciente

Estado: **completado** en el alcance P0 de **H03-SPEC-v1**, con cierre solicitado por el usuario el 2026-11-05. Incluye API y Flutter; dependientes P1 y pantallas administrativas de portal quedan para un alcance posterior.

- [API — plan completado](api/tercer-hito-api/PLAN_completado.md)
- [Flutter — plan completado](app/tercer-hito-app/PLAN_completado.md)
- [Validación y cierre](api/tercer-hito-api/VALIDACION_completado.md)

25/25 pruebas .NET, 25/25 Flutter, PostgreSQL, HTTP/OpenAPI y 2/2 regresiones de citas: PASS. PR, UAT y go/no-go de producción son etapas posteriores.

### Hito 04 — Catálogo y agendas operativas

Estado: completado localmente en el alcance H04-SPEC-v1; Validator PASS. [Informe final](../api/src/Tools/proposals/hitos-04-05/revision-03/VALIDACION-H04.md). UAT y producción permanecen pendientes.

### Hito 05 — Citas y cobertura

Estado: completado localmente en el alcance P0 aprobado de H05-SPEC-v1. [Plan de cierre](hito-05/PLAN_completado.md) y [validación](hito-05/VALIDACION_completado.md). No cierra todos los requisitos de M04/M05 ni habilita integraciones ARS o pagos.

### Hito 06 — Admisión virtual

Estado: **completado en el alcance académico/demo H06-SPEC-v2-DEMO**. Incluye check-in administrativo para paciente adulto titular, consentimiento demo versionado, preflight declarativo, ventana UTC `-30/+15`, estados auditables, idempotencia, concurrencia e invalidación por cancelación/reprogramación. [Plan de cierre](hito-06/PLAN_completado.md), [validación de cierre](hito-06/VALIDACION_completado.md) y [memoria técnica](../docs/M06.md).

El cierre no habilita triaje clínico, diagnóstico, urgencias, tutoría/dependientes, identidad documental, medios, videoconsulta, atención real, portal H06 ni validez legal o clínica de producción.

Los informes de navegador se validan con Playwright. Guardar evidencias dentro del hito correspondiente, sin credenciales, datos reales de pacientes, storageState ni trazas que puedan contener secretos.
