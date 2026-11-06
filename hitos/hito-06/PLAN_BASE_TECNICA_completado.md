# Hito 06 — Base técnica de admisión — completada

**Versión implementada:** H06-SPEC-v1.  
**Estado:** completada localmente en el alcance técnico aprobado el 2026-11-06.  
**Aprobación:** [constancia H06](../aprobacion-humana-hito-06.md).  
**Validación:** [informe de base técnica](VALIDACION_BASE_TECNICA_completado.md).

## Alcance completado

- API consulta requisitos de check-in para una cita y, sin configuración aprobada, devuelve `NotConfigured`, `canProceed=false` y `configurationVersion=null`.
- Patient solo consulta sus propias citas; Admisión tiene lectura limitada; otros roles reciben denegación; sesiones inválidas reciben 401.
- Flutter presenta un aviso neutro de indisponibilidad, sin acciones, captura ni permisos de medios, y limpia el estado al cambiar/cerrar sesión.

## Fuera de este cierre

No están implementados el cuestionario de triaje, derivaciones, consentimiento, tutoría, ventana de check-in, estado `Ready` ni acceso a video. El flujo funcional completo de M06 permanece condicionado a decisiones y aprobaciones clínicas, legales y de producto. Este documento no autoriza activación clínica ni producción.
