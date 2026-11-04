# Plan de trabajo — aplicación de telemedicina en Flutter

**Versión 0.1 · 4 nov 2026 · Propuesta inicial**

## 1. Visión y resultados medibles

Construir una experiencia de telemedicina orientada a la continuidad asistencial. El paciente podrá consultar especialidades y disponibilidad, reservar o reprogramar, verificar la admisión, aceptar consentimiento, acceder a la consulta virtual y consultar indicaciones/documentos y seguimiento.

**Resultados esperados para el piloto:**

- Paciente completa solicitud de cita, confirmación, admisión, consulta y visualización del resumen sin intervención técnica.
- Personal autorizado puede atender la cita, documentar el encuentro y emitir indicaciones.
- Existe trazabilidad de cambios, acceso a datos sensibles, estados y fallos de integración.
- Se ofrecen rutas controladas para cancelaciones, ausencia, falta de cobertura, fallos de conexión y necesidad de atención presencial.

**Indicadores sugeridos, con metas a establecer tras una línea base:** tasa de citas exitosas, abandono durante solicitud, no-show, tiempo de admisión, fallos de conexión, documentos emitidos correctamente y satisfacción posterior. Evitar prometer metas numéricas sin métricas de un piloto.

## 2. Delimitación del MVP

### Incluido P0

1. Flutter Android/iOS: registro, autenticación segura y recuperación de acceso.
2. Perfil y datos esenciales del paciente, alergias/antecedentes autodeclarados y contacto de emergencia.
3. Catálogo de especialidades, médicos y disponibilidad configurada.
4. Reserva, consulta, cancelación/reprogramación de citas con validación de concurrencia.
5. Registro de seguro y estado de validación manual o por integración disponible; atención particular cuando aplique.
6. Cuestionario de admisibilidad, check-in, verificación de identidad y consentimiento versionado.
7. Sala de espera, conexión de videollamada y manejo de reconexión.
8. Portal web básico del médico: agenda, paciente autorizado, notas de encuentro y cierre clínico.
9. Resumen clínico, indicaciones y descarga segura de documentos; receta/órdenes solo cuando el procedimiento de validación/firma esté aprobado.
10. Confirmaciones y recordatorios sin datos clínicos sensibles en el mensaje; seguimiento simple.
11. Portal mínimo de admisión/administración, bitácora de auditoría y soporte.

### No incluido en MVP (P1/P2)

- Integración en línea con múltiples aseguradoras/ARS y reclamaciones automatizadas.
- Pagos con pasarela (pueden procesarse manualmente durante piloto, sujeto a reglas).
- Historia clínica interoperable con múltiples hospitales, importación masiva y FHIR externo completo.
- Receta electrónica interoperable con farmacias, imágenes DICOM y laboratorio bidireccional.
- Seguimiento complejo de enfermedades crónicas, dispositivos IoT, analítica predictiva e IA clínica.
- Multi-tenant completo, marketplace multiinstitución, teleconsulta grupal, grabación de llamadas.
- Chat clínico persistente; WhatsApp/correo solo para notificaciones y enlace autenticado donde se apruebe.

## 3. Entregables

| Entregable | Evidencia de aceptación |
|---|---|
| Descubrimiento | Requisitos priorizados, procesos con excepciones, decisiones regulatorias abiertas/cerradas |
| UX/UI | Flujos y prototipo de pantallas de paciente y portal profesional; pruebas de usabilidad |
| Diseño técnico | Modelo de dominios, API OpenAPI, seguridad y arquitectura de despliegue |
| Flutter | App Android/iOS con flujos MVP, accesibilidad y estados de error |
| Backend | API .NET, persistencia, integraciones, auditoría, jobs y pruebas |
| Portal clínico/admin | Agenda, atención clínica, validación y operaciones esenciales |
| QA y seguridad | Pruebas funcionales, integración, dispositivos, amenaza y regresión |
| Piloto | Despliegue controlado, formación, monitoreo, manual de soporte y rollback |

## 4. Equipo y esfuerzo de referencia

**Supuesto de calendario para planificar:** equipo de 1 desarrollador Flutter y 1 backend full-time (aprox. 30–35 h/sem cada uno), QA parcial a partir de mitad del proyecto, UX parcial y colaboración periódica de un responsable clínico/product owner y referente de cumplimiento. El cronograma de 24 semanas es **estimación inicial**, no compromiso.

Si lo desarrolla una sola persona a tiempo parcial (10–15 h/sem), **24 semanas no es realista** para app + API + portal; estimar por separado una ejecución de **12–18 meses o más**, según complejidad de video, aseguradoras y aprobaciones. Ajustar con velocidad real después de dos sprints.

## 5. Cronograma base — 12 sprints de dos semanas

| Sprint | Semanas | Objetivo | Salida verificable | Dependencias |
|---|---|---|---|---|
| S1 | 1–2 | Descubrimiento, reglas, UX, arquitectura | Backlog firmado, flujos y decisiones clínicas prioritarias | Stakeholders disponibles |
| S2 | 3–4 | Base Flutter/API, CI/CD, auth | Login en ambiente de prueba y roles seguros | Identidad aprobada |
| S3 | 5–6 | Pacientes y catálogo | Perfil, especialidades y médicos disponibles | Datos clínicos mínimos |
| S4 | 7–8 | Disponibilidad y reserva | Reserva transaccional sin doble asignación | Agenda profesional |
| S5 | 9–10 | Gestión de citas, cobertura | Reprogramación; revisión manual de seguro | Reglas de negocio de cobertura |
| S6 | 11–12 | Triage, consentimiento, check-in | Admisión virtual auditable | Validación clínica/legal |
| S7 | 13–14 | Videollamada y sala de espera | Join/reconnect/control de acceso | Contrato con proveedor de video |
| S8 | 15–16 | Encuentro y notas médicas | Portal clínico y cierre de consulta | Flujos clínicos aprobados |
| S9 | 17–18 | Resumen y documentos | Descarga autorizada y versionado | Plantillas/firma aprobadas |
| S10 | 19–20 | Notificaciones y seguimiento | Jobs idempotentes y experiencia postconsulta | Canales contratados |
| S11 | 21–22 | Integración end-to-end y endurecimiento | Regresión, accesibilidad, seguridad, observabilidad | Todos los módulos P0 |
| S12 | 23–24 | UAT, capacitación y piloto | Acta de aceptación, runbooks, salida controlada | Aprobaciones go/no-go |

**Camino crítico:** aprobación de reglas clínicas/consentimiento → proveedor de video → agenda/admisión → encuentro y documentos → prueba de punta a punta → piloto. La validación de seguro externo y facturación automática no deben bloquear el MVP si existe tratamiento manual autorizado.

## 6. Plan por fases

### Fase A — Definición (S1)
- Taller de flujo actual (AS-IS) y flujo esperado (TO-BE), incluyendo rechazos, no cobertura, urgencias, ausencias y fallas.
- Matriz de actores, permisos y tipos de consulta aptos para modalidad remota.
- Diseño inicial de navegación Flutter; diseño de experiencia de médico/admisión.
- Resultado: requisitos con criterios de aceptación y riesgos formalmente asumidos.

### Fase B — Base de plataforma (S2–S3)
- Monorepo o repos separados con API OpenAPI versionada, infraestructura de dev/staging, migraciones, pipeline.
- Arquitectura Flutter por feature, routing, auth, almacenamiento seguro, API client, diseño UI, pruebas unitarias.
- Resultado: autenticación y lectura/edición segura de perfil y catálogo.

### Fase C — Acceso y admisión (S4–S6)
- Agenda, slots, reserva concurrente, cancelaciones; revisión de afiliación y autorización.
- Consentimiento con versión, identificación, triage y estado de check-in.
- Resultado: cita confirmada y paciente autorizado a entrar en sala de espera.

### Fase D — Atención y salida clínica (S7–S9)
- Video + reconexión; notas estructuradas; documentación clínica y permisos.
- Resultado: consulta documentada y documentos disponibles para paciente.

### Fase E — Continuidad, QA y piloto (S10–S12)
- Recordatorios, seguimiento simple, auditoría, seguridad, uso de métricas y operación.
- Resultado: proceso completo validado con casos reales controlados o datos sintéticos según autorización.

## 7. Calidad: Definition of Ready / Done

**Ready:** historia con actor, valor, reglas, dependencia, contrato si aplica, UX/estados vacíos y errores, permisos, criterios testables y datos de prueba no reales.

**Done:** revisión de código, pruebas unitarias + integración pertinentes, no secretos en cliente, logs sin PII/PHI, análisis estático, accesibilidad básica, estados de carga/error, OpenAPI actualizado, métricas, documentación y aceptación en staging.

## 8. Pruebas y validación

- **Unitarias Flutter:** validadores, formatters, view models y estado; pruebas widget en flujos críticos.
- **Backend:** reglas de agenda, control de accesos, concurrencia, estados, idempotencia, documentos.
- **Integración/contrato:** identidad, video, envío de notificaciones, proveedor de seguro (si existe).
- **E2E:** paciente solicita→confirma→check-in→consulta→documentos→seguimiento; variaciones de fallo.
- **Seguridad:** RBAC/ABAC del lado servidor, acceso horizontal a expedientes, gestión de sesión, tokens, almacenamiento y red cifrados, secretos y auditoría.
- **Resiliencia:** pérdida de red, reconexión, push duplicado, servicio externo caído, reintento sin duplicar operaciones.
- **Accesibilidad:** texto escalable, lector de pantalla, contrastes, controles táctiles y permisos de micrófono/cámara.

## 9. Principales riesgos y mitigación

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Video inestable o SDK limitado | Consulta frustrada | POC en S1–S2, proveedor alterno, reconexión, pruebas en red móvil |
| Uso indebido de datos de salud | Alto daño/reputación | Minimización, cifrado, permisos y auditoría, revisión de seguridad antes de piloto |
| Reglas de recetas y consentimiento no definidas | Bloqueo normativo | Revisión legal/clínica temprana; documentos provisionales no emitidos hasta aprobación |
| Doble cita | Conflicto operativo | Restricciones/transacción en BD + pruebas de concurrencia |
| Integración con ARS no disponible | Retraso | Estado revisión manual, sin fingir cobertura confirmada |
| Portal médico insuficiente | MVP incompleto | Diseñarlo desde S1 y presupuestarlo como producto acoplado |
| Cambios de alcance | Retraso/costo | Priorización P0/P1/P2, control de cambios por sprint |

## 10. Decisiones pendientes (deben resolverse)

1. ¿Organización única, red de clínicas o marketplace? ¿Quién es dueño del expediente?
2. ¿Qué especialidades/servicios se habilitan en el piloto, y cuáles excluye el triaje?
3. ¿Paciente puede reservar sin afiliación? ¿Cómo paga consulta particular?
4. ¿Hay ARS que ofrezca APIs? ¿Cómo se documentará la validación manual?
5. ¿Qué constituye identidad válida y firma/consentimiento válido según evaluación jurídica local?
6. ¿Qué proveedor de video, residencia de datos, DPA, retención y telemetría se autorizan?
7. ¿Documentos: resumen e indicaciones solamente, o receta/órdenes formalmente válidas?
8. ¿Portal administrativo propio o integración con sistema hospitalario existente?
9. ¿Quién atiende urgencias y gestiona derivaciones y fallos de consulta?
10. ¿Indicadores, presupuesto, número de usuarios concurrentes y niveles de servicio objetivo?

## 11. Hitos de aprobación (go/no-go)

- **G1 / fin S1:** flujos, clínica, arquitectura y backlog P0 aprobados.
- **G2 / fin S6:** paciente puede reservar y completar check-in; seguridad básica validada.
- **G3 / fin S9:** consulta/documentos completados en staging con datos sintéticos.
- **G4 / fin S11:** pruebas E2E, seguridad, restauración y procedimiento de soporte satisfactores.
- **G5 / fin S12:** responsable clínico, técnico y legal autorizan piloto y alcance de datos reales.
