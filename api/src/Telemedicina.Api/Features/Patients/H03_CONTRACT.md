# Perfil propio — H03-SPEC-v1

GET /api/v1/patients/me conserva data:null sin perfil. Con perfil devuelve data con profile
(campos existentes), version autoritativa, updatedAt UTC, source:SelfDeclared,
declarationMetadata:{allergies,medications,conditions}, cada categoría con source:SelfDeclared
y updatedAt:timestamp UTC|null, y completeness:{status:Complete|Incomplete,missingFields:string[]}.
La fecha nula significa desconocida; listas vacías significan Sin datos registrados.

GET /api/v1/patients/me/completeness devuelve data:{status,missingFields} incluso sin perfil.
Campos ordenados: firstName,lastName,birthDate,phone,address,emergencyName,emergencyPhone,
allergies,medications,conditions. Completo no habilita admisión ni verifica identidad.

PATCH /api/v1/patients/me reemplaza todos los campos de ProfileRequest y requiere version
explícita. 200 data:{version}. 400 INVALID_PROFILE para reglas; 400 VALIDATION_FAILED para
binding. errors es string[] con campo:REQUIRED|INVALID|MAX_LENGTH|MAX_ITEMS sin valores.
401 sin sesión válida; 403 rol distinto de Patient; 409 VERSION_CONFLICT sin sobrescritura.
Los atributos ProducesResponseType publican respuestas tipadas en /openapi/v1.json.
Propiedades protegidas y metadatos calculados no son editables. Teléfono no modifica credenciales.
No hay permisos nuevos para portal, tutores ni personal.

Migración 006 aditiva, explícita e idempotente, no modifica body ni version ni rellena fechas.
Guardar conserva transacción, revalidación de sesión, bloqueo y auditoría sin contenido clínico.
