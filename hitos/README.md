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

Los informes de navegador se validan con Playwright. Guardar evidencias dentro del hito correspondiente, sin credenciales, datos reales de pacientes, storageState ni trazas que puedan contener secretos.
