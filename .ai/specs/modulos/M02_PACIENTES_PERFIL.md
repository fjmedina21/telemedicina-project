# M02 — Perfil de paciente y dependientes

**Prioridad:** P0 (perfil), P1 (dependientes completos) · **Actores:** paciente, tutor, admisión, médico · **Depende de:** M01

## Objetivo
Crear una identidad clínica única y permitir que la información mínima de contacto y antecedentes esté disponible en la atención pertinente.

## Funciones e historias
- US-05: Como paciente, edito datos de contacto y dirección con validación.
- US-06: Como paciente, declaro alergias, medicamentos habituales y antecedentes relevantes, diferenciando lo autodeclarado de lo validado clínicamente.
- US-07: Como tutor, gestiono dependientes solo tras verificación del vínculo y permisos.
- US-08: Como admisión, localizo y resuelvo posibles duplicados sin combinar expedientes automáticamente.

## Pantallas Flutter
Mi perfil; datos de contacto; datos médicos declarados; seguro vinculado (M05); contactos de emergencia; dependientes (P1).

## Reglas
- No permitir cambios silenciosos de identificador legal verificado; usar flujo de corrección con auditoría.
- No almacenar más información de salud que la necesaria para el servicio; distinguir fuente/fecha de cada dato.
- Existen diferencias entre titular de la cuenta, paciente atendido y responsable legal.
- No fusionar registros sin revisión humana y procedimiento trazable.

## Datos mínimos
`Patient(id, userId?, firstName, lastName, birthDate, documentType?, documentLast4?, verifiedAt?, status)`; `ContactInfo`; `EmergencyContact`; `HealthDeclaration(patientId, category, value, source, updatedAt)`; `GuardianLink` (P1).

## API candidata
`GET/PATCH /patients/me`, `GET/PUT /patients/me/health-declarations`, `GET/POST /patients/me/dependents` (P1), recursos protegidos para admisión.

## Excepciones
Perfil incompleto, documento duplicado, edad no habilitada, cambio de teléfono no verificado, tutor con autorización vencida.

## Criterios de aceptación
1. El paciente modifica contacto y visualiza nueva información; el servidor registra quién y cuándo.
2. Datos autodeclarados nunca aparecen como diagnóstico confirmado.
3. Un usuario no puede acceder a datos de dependientes ajenos.
4. Si faltan datos obligatorios, el check-in identifica exactamente qué información completar sin revelar datos innecesarios.
