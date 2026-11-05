# Plan de ejecución — segundo hito (transversal)

**Estado:** completado en alcance funcional local el 2026-11-05. Evidencia: [validación H02](../../../api/src/Tools/validation/H02-2026-11-05/VALIDACION.md). PR, UAT y go/no-go de piloto siguen fuera del cierre local.  
**Módulos:** M01 Identidad y acceso, M12 Operación y auditoría (mínimo).  
**Áreas:** `api/src/`, `app/src/`, `portalweb/src/`.  
**Base:** primer hito usable ya integrado localmente. Este documento no afirma que el segundo hito esté implementado.

## Objetivo del incremento

Habilitar una base segura y comprobable para las cuentas sintéticas de los actores del piloto. El servidor será la autoridad para roles, alcance organizacional y permisos; el portal permitirá operar el ciclo básico de cuentas de personal, y la app/portal mostrarán únicamente las capacidades autorizadas. Debe ser posible validar cuentas y rechazos de acceso con datos sintéticos, sin abrir acceso a expedientes o introducir flujos clínicos.

## Incluido

### P0 — Identidad y operación de cuentas

1. Definir y documentar una matriz de roles y permisos con denegación por defecto. Roles candidatos de las specs: paciente, tutor, médico/profesional, admisión, coordinador, soporte, auditor y administrador. La matriz final necesita aprobación de Product Owner y responsables de seguridad; no se habilita ningún rol solo por aparecer en esta lista.
2. Extender el modelo API para asignaciones de rol y, si aplica, ámbito organizacional; un usuario no puede elevar sus permisos ni asignarse roles a sí mismo.
3. Mantener autenticación de personal por contraseña, según la decisión vigente del primer hito. Añadir límites de intentos, revocación de sesiones y errores que no revelen si una cuenta existe. La suficiencia de este método para producción queda sujeta a revisión de seguridad.
4. Crear ciclo de vida administrativo para cuentas de personal: alta/invitación, activación, suspensión y revocación de sesiones. Toda acción debe identificar actor, recurso, resultado y fecha en auditoría, sin registrar contraseñas ni tokens.
5. Preparar cuentas sintéticas locales/de desarrollo para los roles aprobados, sin credenciales en el repositorio, documentación pública, capturas ni logs. Los datos y mecanismos de seed deben estar deshabilitados fuera de Development.
6. Hacer que `/auth/me` (o contrato equivalente revisado) informe identidad y permisos efectivos mínimos para que los clientes representen el acceso; el servidor seguirá validando cada operación.
7. Incluir pruebas de autorización negativa: cambio de rol por usuario, acceso horizontal, cuenta suspendida, sesión revocada, rol sin permiso y acceso de soporte a contenido clínico.
8. Conservar las capacidades del primer hito que no contradigan la matriz aprobada.

### P1 condicionado — Tutor y separación clínica

- Incorporar rol de tutor solo después de definir el proceso verificable para establecer, revisar, vencer y revocar el vínculo con un dependiente.
- No dar al rol médico acceso a pacientes, notas o documentos en este hito. Un futuro incremento debe definir relación de atención y permisos clínicos antes de exponerlos.
- El rol de auditor podrá consultar eventos de auditoría permitidos; no obtiene lectura clínica por esa condición.
- Soporte podrá gestionar únicamente metadatos técnicos/incidencias que se aprueben, nunca contenido de video, notas o documentos por defecto.

## Fuera de alcance

- Agenda profesional, reservas y políticas de cancelación/reprogramación.
- Perfil clínico de dependientes, verificación legal de tutoría o representación.
- Acceso a expedientes, notas, triaje, consentimiento clínico, videollamadas, recetas o documentos.
- MFA, SSO, federación de identidad o decisión de que la contraseña sea suficiente para producción.
- Integraciones reales de correo/ARS/pagos, uso de datos reales, despliegue o cambios de infraestructura.
- Definir elegibilidad clínica, consentimiento, validación profesional/legal o políticas de retención.

## Criterios de aceptación

1. Existe una matriz aprobada que especifica acciones permitidas y denegadas por rol y ámbito, incluidos paciente, admisión, profesional, soporte y administrador; tutor/auditor/coordinador se habilitan solo con sus reglas aprobadas.
2. Las cuentas sintéticas de cada rol aprobado pueden autenticarse en el entorno de desarrollo y sus permisos se verifican en la API. No hay secretos sintéticos versionados ni cuentas de prueba habilitadas en producción.
3. Un usuario no puede asignarse roles, modificar asignaciones fuera de su permiso ni ampliar su ámbito; llamadas no autorizadas responden de forma consistente sin exponer datos.
4. Suspender una cuenta o revocar una sesión impide continuar usando sesiones/tokens invalidados.
5. El portal permite a un administrador autorizado administrar el ciclo de vida acordado; cada acción queda auditada y la UI no sustituye autorización de servidor.
6. Flutter conserva el acceso del paciente y maneja sesión/roles de forma segura; no guarda tokens nuevos en almacenamiento inseguro ni renderiza funciones no autorizadas.
7. Soporte no puede leer información clínica; probar el rechazo en servidor, no solo la ausencia de botones.
8. Auditoría no incluye contraseñas, códigos de verificación, tokens, secretos ni texto clínico completo.
9. La documentación OpenAPI y los contratos compartidos reflejan las decisiones aprobadas, y las guías de ejecución usan exclusivamente datos sintéticos.
10. Revisión de PR, validación de seguridad y UAT de acceso con representantes designados antes de marcar el hito como completado.

## Secuencia y entregables

| Paso | Responsable de área | Entregable |
|---|---|---|
| 1. Confirmar política | Product Owner + seguridad; consulta clínica/legal donde aplique | Matriz de roles, límites y decisiones aprobadas |
| 2. Diseño técnico | Arquitectura | Modelo de asignaciones, límites de organización, endpoints, auditoría y amenazas |
| 3. Contratos | API + clientes | OpenAPI/DTOs y estados de cuenta acordados antes de integrar clientes |
| 4. API | Backend, `api/src/` | Asignaciones, autorización, ciclo de vida, auditoría y seed sintético Development |
| 5. Portal | Portal, `portalweb/src/` | Operaciones de cuenta permitidas y representación de permisos |
| 6. App | Flutter, `app/src/` | Manejo coherente de sesión/permisos para el rol paciente y estados de acceso |
| 7. Validación | Validator + responsables de PR | Matriz positiva/negativa, revisión Playwright donde aplique, evidencias sintéticas y UAT |

La ejecución sigue el handoff local: orquestador → arquitecto → creador de specs → implementador → validador. Backend tiene un responsable; Flutter requiere coordinación de ambos colaboradores. La integración se revisa por PR.

## Dependencias y riesgos

- **Bloqueante:** aprobación de la matriz de roles, especialmente quién puede crear administradores y asignar roles.
- **Bloqueante para tutor:** definición del método para verificar el vínculo y tratar cambios, vencimiento y disputas; no inferirlo de la cuenta.
- **Riesgo de seguridad:** retirar el segundo factor deja pendiente una evaluación explícita del riesgo para cuentas de personal antes de producción.
- **Riesgo de alcance:** cuentas autenticables no equivalen a módulos clínicos habilitados; mantener autorización denegada para funcionalidades no aprobadas.
- **Dependencia técnica:** revisar migraciones y sesiones existentes antes de modificar el modelo; conservar compatibilidad con el primer hito.
- **Privacidad:** datos exclusivamente sintéticos, mínimos y no derivados de pacientes reales.

## Handoff al arquitecto

Proponer contrato de roles y permisos del lado API, ciclo de invitación/activación/suspensión, invalidación de sesiones, registro de auditoría y mecanismo de seed de desarrollo. Especificar códigos RFC 7807, concurrencia al modificar asignaciones, respuesta mínima de identidad y matriz de pruebas. Presentar alternativas para los puntos sin decisión; no habilitar permisos clínicos por inferencia.

## Checklist de aprobación

- [ ] Matriz de roles/permisos aprobada por Product Owner y seguridad.
- [ ] Política de alta de administrador y ámbito organizacional aprobados.
- [ ] Decisión de MFA evaluada para producción; si queda pendiente, el hito no declara producción lista.
- [ ] Reglas de tutoría verificable aprobadas antes de habilitar ese rol.
- [ ] Diseño técnico y OpenAPI revisados antes de implementar clientes.
- [ ] Evidencia de pruebas negativas, revisión PR y UAT documentada.
- [ ] Confirmación de que seeds, cuentas, capturas y logs no contienen secretos ni datos reales.

## Decisiones abiertas

1. ¿Qué roles se habilitan realmente en el segundo hito y cuáles quedan solo definidos para el futuro?
2. ¿Quién invita/activa personal y cómo se verifica su identidad profesional?
3. ¿Se manejará una sola organización en este incremento o habrá ámbito organizacional desde el inicio?
4. ¿Qué método de autenticación adicional, si alguno, se exige para personal antes de producción?
5. ¿Qué campos mínimos de cuenta puede consultar admisión, soporte y auditoría?
