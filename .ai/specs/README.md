# Telemedicina — plan y especificaciones funcionales/técnicas

**Versión:** 0.1 (borrador para validación)  
**Fecha:** 4 de noviembre de 2026  
**Estado:** propuesta; requiere revisión del patrocinador, responsables clínicos, seguridad y cumplimiento.

## Objetivo

Desarrollar una aplicación móvil Flutter para pacientes que permita solicitar consultas de telemedicina, gestionar citas, realizar admisión virtual, participar en videoconferencias, recibir documentos clínicos y dar seguimiento al plan de atención. El producto se apoya en una API y en un portal web mínimo para médicos y admisión; este último no se implementa en Flutter dentro de la propuesta base.

## Documentos

- [Plan de trabajo](PLAN_TRABAJO.md): alcance, entregables, cronograma, capacidad y riesgos.
- [Arquitectura y contratos](ARQUITECTURA_Y_CONTRATOS.md): decisiones técnicas, dominios, estados, datos y API base.
- [Especificaciones por módulo](modulos/): 12 archivos, con historias, reglas, pantallas, datos, API, errores y pruebas de aceptación.
- [Backlog y trazabilidad](BACKLOG_Y_TRAZABILIDAD.md): prioridades, dependencias, pruebas E2E, mapeo de los 10 pasos originales.

## Leyenda de prioridades

- **P0 (MVP):** imprescindible para un piloto controlado.
- **P1:** siguiente entrega; operativamente valioso, pero no bloquea el piloto si existe alternativa controlada.
- **P2:** evolución posterior.

## Supuestos que requieren confirmación

1. Piloto inicial en una organización de salud de República Dominicana, moneda DOP y horario America/Santo_Domingo.
2. Pacientes móviles en Android/iOS. Personal clínico/administrativo mediante portal web mínimo.
3. Un único prestador/organización en MVP, con estructura de datos preparada para varios centros posteriormente.
4. Agendamiento y admisión parcialmente automáticos; el seguro puede verificarse manualmente en MVP.
5. El MVP **no diagnostica con IA**, **no sustituye emergencias**, **no graba consultas por defecto** y **no emite recetas legalmente válidas sin revisión normativa y firma profesional autorizada**.
6. Para un despliegue con datos reales se requiere revisión legal, clínica y de ciberseguridad, además de convenios/contratos con proveedores externos.

## Criterio para iniciar desarrollo

Cerrar los puntos del apartado «Decisiones pendientes» del plan y obtener aprobación de las reglas clínicas de admisibilidad de consultas remotas. Hasta entonces, los contratos de API son **propuestas versionables**, no interfaces firmadas.
