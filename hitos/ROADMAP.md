# Hoja de ruta de hitos — Telemedicina

**Alcance:** primera versión usable y evolución del MVP de los módulos M01–M12.  
**Estado:** hitos 01 y 02 completados en su alcance funcional local; UAT, PR y go/no-go de producción permanecen separados.

## Resumen

| Hito | Módulos | Resultado verificable | Dependencias | Estado |
|---|---|---|---|---|
| 01. Base usable de paciente y citas | M01–M05 parcial, M12 mínimo | Paciente verificado explora catálogo y solicita/gestiona citas; admisión revisa solicitudes; auditoría básica | Plataforma y PostgreSQL | **Completado en alcance usable** |
| 02. Roles y operación de cuentas | M01, M12 | Roles autorizados, cuentas sintéticas de personal y ciclo de vida auditable | Hito 01; matriz de permisos aprobada | **Completado** |
| 03. Perfil completo y dependientes | M02 | Perfil/contacto y declaraciones del paciente con fuente; dependientes solo con vínculo verificado | Hito 02; política de tutoría para dependientes | Por iniciar |
| 04. Catálogo y agendas operativas | M03 | Servicios, profesionales y horarios configurables; disponibilidad coherente | Hito 02; reglas de oferta y horarios | Por iniciar |
| 05. Citas y cobertura | M04, M05 | Políticas aprobadas de reserva/cambio; revisión manual de cobertura sin afirmar autorización no recibida | Hitos 03–04; reglas administrativas y de cobertura | Por iniciar |
| 06. Admisión, triaje y consentimiento | M06 | Check-in con estados auditables según reglas aprobadas | Hito 05; aprobación clínica y legal | Condicionado |
| 07. Videoconsulta | M07 | Sala de espera, acceso temporal, unión/reconexión y fallos controlados | Hito 06; proveedor y revisión de privacidad/seguridad | Condicionado |
| 08. Encuentro e historia clínica | M08 | Profesional autorizado documenta y cierra el encuentro con historial trazable | Hitos 02, 03 y 07; flujo clínico aprobado | Condicionado |
| 09. Documentos clínicos | M09 | Publicación y descarga autenticada de documentos autorizados y versionados | Hito 08; política de documentos y firma aprobada | Condicionado |
| 10. Notificaciones y seguimiento | M10 | Confirmaciones/recordatorios idempotentes y seguimiento sin contenido clínico sensible expuesto | Hitos 05 y 08; canales autorizados | Por iniciar |
| 11. Facturación y reclamaciones | M11 | Registro económico trazable; automatización solo con reglas e integraciones aprobadas | Hito 05; decisiones financieras/ARS | P1, después del MVP |
| 12. Endurecimiento y piloto controlado | M12 transversal | Regresión E2E, seguridad, operación, UAT, capacitación y go/no-go documentado | Hitos P0; responsables disponibles y políticas aprobadas | Por iniciar |

## Alcance de cada hito

### 01. Base usable de paciente y citas — completado

Incluye registro/verificación, acceso y recuperación, perfil básico autodeclarado, catálogo/disponibilidad inicial, solicitud y manejo básico de citas, revisión de admisión, marca compartida y auditoría mínima.

Los informes del incremento reportan PASS con datos sintéticos. El estado completado aplica únicamente al alcance usable implementado; no certifica producción ni cierra cobertura externa, correo de producción, agenda administrativa completa, pruebas móviles nativas, CI/UAT ni otros módulos aún pendientes.

### 02. Roles y operación de cuentas

Continuar con el [plan API](api/segundo-hito-api/PLAN.md), [plan Flutter](app/segundo-hito-app/PLAN.md) y [plan Portal Web](portalweb/segundo-hito-portalweb/PLAN.md). El orquestador coordina las etapas; la spec del hito requiere aprobación humana antes de implementación. Habilitar solo roles y acciones aprobados, con datos sintéticos, autorización en servidor, suspensión/revocación y auditoría. No habilita acceso clínico por sí solo.

### 03. Perfil completo y dependientes

Completar contacto y datos autodeclarados con procedencia/fecha y presentación segura. Separar titular de cuenta y persona atendida. No habilitar acceso de tutor ni dependientes hasta aprobar cómo se verifica, revisa, vence y revoca el vínculo.

### 04. Catálogo y agendas operativas

Completar gestión de servicios habilitados, profesionales, agendas, duración, zonas horarias, bloqueos y disponibilidad. La administración no puede publicar automáticamente servicios sin aprobación correspondiente. Disponibilidad informativa y reserva final validada transaccionalmente.

### 05. Citas y cobertura

Definir anticipación, cancelación, reprogramación, no-show y atención particular; estados de cobertura, evidencia y vigencia; concurrencia, idempotencia e historial. Una revisión manual nunca debe presentarse como aprobación de la aseguradora.

### 06. Admisión, triaje y consentimiento

Implementar solo reglas y textos revisados por responsables clínicos y legales. La falta de consentimiento o los criterios de exclusión impiden avanzar al estado autorizado que se apruebe. No inventar triage ni recomendaciones clínicas.

### 07. Videoconsulta

Seleccionar proveedor mediante evaluación y acuerdo aplicable. Usar tokens efímeros, autorización por cita, controles de reconexión y contingencia. Grabación deshabilitada salvo aprobación explícita de política y consentimiento.

### 08. Encuentro e historia clínica

Definir campos, autoría, correcciones, firma/cierre, relación profesional-paciente y auditoría. El acceso queda limitado a la relación de atención autorizada. No redactar decisiones clínicas automáticamente.

### 09. Documentos clínicos

Definir qué documentos puede emitir cada rol, quién los firma, qué versiones se conservan y cómo se descargan. Recetas u órdenes se habilitan únicamente tras aprobar requisitos clínicos y legales.

### 10. Notificaciones y seguimiento

Completar outbox, deduplicación, reintentos, preferencias y estados de entrega. Mensajes externos neutros, sin diagnósticos ni detalle clínico. Validación con proveedores autorizados antes de producción.

### 11. Facturación y reclamaciones

Hito P1: modelar cargos, pagos, devoluciones y reclamaciones con estados separados de la cita. Empezar por trazabilidad manual si se aprueba; no integrar pasarelas ni ARS sin proveedor, contrato y reglas definidos. No bloquea el MVP cuando se acuerde una alternativa manual.

### 12. Endurecimiento y piloto controlado

Completar revisión de permisos y amenazas, auditoría, copias/restauración, observabilidad, CI, accesibilidad, E2E con datos sintéticos, UAT y procedimientos de soporte/rollback. Datos reales solo tras autorización y go/no-go de responsables técnicos, clínicos y legales.

## Regla para marcar un hito como completado

Un hito solo se marca completado cuando sus criterios de aceptación fueron revisados, la evidencia de validación está guardada, Validator documentó el resultado y la persona responsable aprobó el cierre. Entonces se añade `_completado` al nombre de los documentos principales del hito, antes de la extensión: `PLAN_completado.md`, `VALIDACION_completado.md`. No renombrar una propuesta, evidencia o archivo de código para sugerir un cierre que no ocurrió. Mantener los enlaces actualizados.

Para el hito 01 ya existente, los informes de validación completados llevan ahora `_completado`. Los planes del hito 02 y todos los siguientes conservan su nombre normal hasta que terminen.
