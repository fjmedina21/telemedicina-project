# Backlog transversal, trazabilidad y pruebas E2E

## 1. Matriz de módulos

| ID | Módulo | MVP | Depende de | Resultado |
|---|---|---|---|---|
| M01 | Identidad y acceso | P0 | Infraestructura | Sesiones y permisos seguros |
| M02 | Pacientes y perfil | P0 | M01 | Identidad clínica mínima |
| M03 | Catálogo médico | P0 | M01 | Atención visible y oferta disponible |
| M04 | Citas | P0 | M02, M03 | Reserva sin solapamiento |
| M05 | Seguros/cobertura | P0 manual / P1 API | M02, M04 | Elegibilidad explícita |
| M06 | Triaje, consentimiento y admisión | P0 | M04, M05 | Paciente listo para consulta |
| M07 | Video y sala de espera | P0 | M06 | Encuentro remoto seguro |
| M08 | Historia/encuentro | P0 | M07, M02 | Nota y resumen clínicos |
| M09 | Documentos | P0 básicos / P1 regulados | M08 | Documentos publicados |
| M10 | Notificaciones y seguimiento | P0 básico / P1 avanzado | M04, M08 | Continuidad asistencial |
| M11 | Cobros y reclamaciones | P0 registro manual si aplica / P1 pasarela | M04, M05 | Trazabilidad económica |
| M12 | Operación y auditoría | P0 mínimo | Transversal | Operación segura |

## 2. Trazabilidad de la imagen «Proceso integral»

| Etapa original | Requisitos principales | Módulos involucrados |
|---|---|---|
| 1 Solicitud de cita | Solicitud multicanal; autoservicio; motivo | M03, M04, M12 |
| 2 Recepción Hospital/PSS | Datos mínimos, alta, revisión administrativa | M02, M12 |
| 3 Validación con seguro | Afiliación, cobertura y autorización | M05, M12 |
| 4 Agendamiento | Horario, confirmación y conflictos | M03, M04 |
| 5 Confirmación | Mensaje, instrucciones, recordatorio | M04, M10 |
| 6 Consentimiento y admisión | Triaje, identidad, firma/aceptación | M01, M02, M06 |
| 7 Consulta de telemedicina | Sala y conexión, expediente, nota | M07, M08 |
| 8 Resultado | Resumen, indicaciones y documentos | M08, M09 |
| 9 Cierre administrativo | Comprobante, reclamación, auditoría | M11, M12 |
| 10 Seguimiento | Recordatorios, control, satisfacción | M10, M12 |

## 3. Casos E2E obligatorios del MVP

### E2E-01: atención exitosa
1. Paciente verificado inicia sesión.
2. Elige especialidad y médico; reserva un slot.
3. Cita queda en validación y admisión/seguro aprueba o registra atención particular autorizada.
4. Paciente recibe confirmación e instrucciones neutras.
5. Completa preguntas de admisibilidad, identidad y consentimiento vigente.
6. Ingresa a sala de espera; médico se une y efectúa atención.
7. Médico firma/cierra nota; se publica resumen autorizado.
8. Paciente recibe notificación y accede al documento autenticado.
9. Se programa siguiente control o se ofrece encuesta.

### E2E-02: conflicto de reserva
Dos pacientes seleccionan el mismo horario de profesional. Resultado: solo uno confirma, el otro recibe `409` y alternativas; no se crean citas duplicadas ni se envían dos confirmaciones.

### E2E-03: cobertura denegada
Validación devuelve `Denied` o revisión manual negativa. Resultado: no se comunica que está cubierta; se ofrece alternativa permitida (particular/reprogramar/cancelar), sin compromiso económico oculto.

### E2E-04: triaje excluyente o consentimiento rechazado
Paciente reporta criterio definido clínicamente o no consiente. Resultado: sistema no habilita video, registra estado, presenta instrucciones aprobadas y canal para atención alternativa.

### E2E-05: caída de conexión
Video se interrumpe y se reintenta dentro de ventana autorizada. Resultado: no se crea segunda consulta; estado técnico y nota clínica se mantienen consistentes, existe escalamiento a soporte.

### E2E-06: acceso indebido
Paciente A intenta descargar documento de B o médico no asignado intenta leer una nota. Resultado: rechazo uniforme, registro de evento relevante y sin fuga de metadatos.

### E2E-07: cancelación y recordatorios
Paciente cancela cita confirmada. Resultado: slot liberado según política, cambio auditado, próximos recordatorios cancelados y estado visible en app/portal.

### E2E-08: errores/reintentos
API o proveedor de notificaciones repite mismo evento. Resultado: reserva, cobro y mensaje no se duplican; error técnico resulta recuperable.

## 4. Definition of Ready por historia

- [ ] Actor, necesidad y resultado.
- [ ] Módulo propietario y servicios externos involucrados.
- [ ] Estados y permisos válidos.
- [ ] Reglas, validaciones, campos y contrato de API.
- [ ] Diseño de pantalla con carga, vacío, error y éxito.
- [ ] Dependencias resueltas o simulables mediante mock.
- [ ] Criterios de aceptación verificables.
- [ ] Consideraciones de privacidad/cumplimiento identificadas.

## 5. Definition of Done por historia

- [ ] Compila en Android/iOS objetivo y en CI.
- [ ] Pruebas unitarias, de widget/integración y contrato apropiadas.
- [ ] API con autorización y validaciones de servidor, pruebas negativas incluidas.
- [ ] Revisión de PR y comprobación de logs sin datos de salud.
- [ ] Traducciones/localización, accesibilidad, permisos y estados de error.
- [ ] Auditoría y métricas relevantes sin información sensible.
- [ ] OpenAPI y especificación actualizados.
- [ ] Demo aprobada por product owner/responsable clínico si aplica.

## 6. Priorización por primer incremento usable

1. Infraestructura + M01 + M02 + M03: usuario puede iniciar sesión, completar perfil, explorar oferta.
2. M04 + M05 (manual) + M12 (cola mínima): una cita se puede pedir y aprobar.
3. M06 + M07: check-in, consentimiento y videollamada.
4. M08 + M09 (resumen/indicaciones) + M10: ciclo clínico completo.
5. Endurecimiento y piloto: pruebas E2E, privacidad, seguridad, dispositivos, capacitación y soporte.

## 7. Aspectos que requieren prototipo temprano

- **Spike Video:** privacidad contractual, tokens efímeros, join y reconexión Android/iOS, calidad en red móvil, métricas.
- **Spike de identidad:** verificación de paciente/tutor y cuentas de admisión.
- **Spike de cobertura:** modelo de autorización real con al menos una ARS o definir operación manual.
- **Spike de firma:** qué documentos son clínicamente y legalmente publicables y con qué evidencias.

## 8. Plan de UAT

Validar con al menos un representante de paciente, admisión, médico y facturación; usar datos sintéticos hasta tener autorización. La aceptación incluye funcionalidad, accesibilidad, privacidad, trazabilidad, seguridad, estabilidad y procedimientos de contingencia. Documentar hallazgos con criticidad, reproducción, corrección y cierre.
