# M09 — Recetas, órdenes, indicaciones, referimientos y documentos

**Prioridad:** P0 resumen e indicaciones; P1 receta/órdenes legalmente operativas · **Actores:** médico, paciente, auditor clínico · **Depende de:** M08

## Objetivo
Emitir y entregar resultados clínicos de manera estructurada, versionada, segura y conforme a procedimientos aprobados.

## Funciones e historias
- US-33: Como médico, genero resumen e indicaciones desde el encuentro.
- US-34: Como paciente, veo y descargo únicamente documentos publicados para mí.
- US-35 (P1): Como médico, emito receta, orden de laboratorio/imágenes o referimiento con firma y campos obligatorios.
- US-36 (P1): Como paciente, identifico qué documentos están pendientes de validación y cuáles son definitivos.

## Pantallas Flutter
Mis documentos; documentos por consulta; detalle/vista previa; descargar/compartir con aviso sobre privacidad.

## Reglas
- Cada documento tiene tipo, encuentro fuente, autor, estado, versión, fecha y validez.
- Documentos firmados no se modifican: reemplazo/anulación auditados y nueva versión.
- Descarga con autorización en backend y token de acceso de corta vida; almacenar en dispositivo solo por acción explícita.
- Receta, órdenes y firma clínica **no se activan en producción** hasta validación legal/profesional y modelo de firma.
- Intercambio con farmacia/laboratorio y estándares interoperables queda fuera del MVP.

## Datos mínimos
`ClinicalDocument(id, encounterId, patientId, type, status, currentVersion, publishedAt?)`; `DocumentVersion(id, documentId, storageKey, checksum, authorId, signedAt?, signatureRef?)`; `DocumentAccessEvent`.

## API candidata
`GET /encounters/{id}/documents`, `GET /documents/{id}`, `GET /documents/{id}/download`, `POST /encounters/{id}/documents`, `POST /documents/{id}/publish`, `POST /documents/{id}/void` (servidor: profesional autorizado).

## Excepciones
Documento no publicado, intento de acceso cruzado, firma fallida, archivo corrupto, descarga vencida, documento anulado.

## Criterios de aceptación
1. Paciente B no puede descargar documento de A aunque conozca URL/ID.
2. Archivo publicado corresponde al hash/versión registrado.
3. Documento anulado se muestra como tal; no desaparece del rastro de auditoría.
4. Generación fallida no marca documento como publicado.
