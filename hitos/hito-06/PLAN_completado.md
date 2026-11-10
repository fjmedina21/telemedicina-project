# H06-SPEC-v2-DEMO — Plan de cierre

**Estado:** completado en alcance académico/demo. Datos sintéticos.

## Alcance cerrado

- Paciente adulto titular (`Self`); sin tutoría ni `GuardianLink`.
- Check-in administrativo no clínico desde Flutter.
- Consentimiento demo v1 con texto y hash fijos.
- Ventana UTC de 30 minutos antes a 15 minutos después del inicio.
- Estados `NotStarted`, `InProgress`, `Verified`, `Ready`, `Rejected` y `Expired`.
- `Ready` calculado exclusivamente por el backend.
- Invalidación por cancelación o reprogramación.
- Idempotencia y protección de concurrencia.
- Vista `ready_check_ins` para consumo posterior.

## Fuera de alcance

- Triaje clínico, diagnóstico, urgencias o recomendaciones.
- Tutoría/dependientes, identidad documental y biometría.
- Cámara, micrófono, video, grabación o documentos.
- Pantalla específica del portal para H06.
- Uso con pacientes reales o validez legal/clínica de producción.

## Implementación

- Rama: `feature/tunning-h06`.
- Commit de integración: `d4f3f06`.
- Migración: `010_academic_check_in.sql`.
- Documentación técnica: [`docs/M06.md`](../../docs/M06.md).
