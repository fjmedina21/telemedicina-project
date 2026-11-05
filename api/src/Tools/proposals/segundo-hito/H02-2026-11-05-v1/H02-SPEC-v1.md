# H02-SPEC-v1 — Roles y operación de cuentas
Ejecución H02-2026-11-05-v1. Fuente: diseño Architect H02-v1 y entrega Spec Creator de esta conversación; aprobada por el usuario el 2026-11-05.
Esta copia conserva el alcance aprobado; la implementación es una propuesta separada.

## Alcance aprobado
Una organización implícita y un rol por cuenta.
Roles Patient, Admission, Practitioner, Support, Administrator.
Conservar capacidades existentes de paciente y admisión, incluida suspensión/reactivación de pacientes por admisión.
Administrator gestiona exclusivamente personal Admission, Practitioner, Support; no hereda Admission ni administra Administrator por HTTP.
Practitioner y Support: identidad propia, renovación/cierre de sesión y pantalla sin funciones operativas.
Tutor, auditor, coordinador, multiorganización y nuevos accesos clínicos quedan aplazados.
Bootstrap del primer administrador y cuentas sintéticas solo Development, configuración privada.
Invitaciones mediante buzón privado de desarrollo, sin nuevos envíos SMTP reales.
Personal conserva autenticación por contraseña; MFA y suficiencia para producción quedan pendientes.

## Historias
- H02-API-01: identidad incluye permisos efectivos y autorización real del servidor.
- H02-API-02: administrador lista/invita personal permitido y destinatario activa por invitación.
- H02-API-03: cambio de rol, suspensión/reactivación y revocación con versión y auditoría.
- H02-API-04: suspensión/revocación serializadas frente a login/refresh.
- H02-API-05: seed y mecanismo de invitación sintéticos rechazados fuera de Development.
- H02-PORTAL-01: pantallas protegidas, confirmaciones y conflictos explícitos.
- H02-PORTAL-02: preservar admisión; profesional/soporte sin funciones operativas.
- H02-APP-01: preservar paciente; limpiar sesión y estado sensible ante invalidación.
- H02-QA-01: matriz API, regresión, concurrencia, Flutter y Playwright con evidencia saneada.

## Matriz
- Denegación por defecto para acciones no concedidas.
- Todos los roles: identidad/sesión propias.
- Patient: funciones actuales propias.
- Admission: operación actual, suspensión/reactivación de pacientes, auditoría previa conservada.
- Administrator: listar/invitar/cambiar roles/estado/revocar sesiones de personal permitido; auditoría saneada de personal.
- Nadie: administrar Administrator por HTTP ni nuevos accesos clínicos.
- Permisos nuevos: staff.read,staff.invite,staff.role.change,staff.state.change,staff.sessions.revoke,staff.audit.read.
- El cliente nunca concede rol/permiso/ámbito.

## Datos y estados
- Cuenta visible: id,email,role,status,version.
- Correo normalizado <=254 caracteres; contraseña 12–128; UUID válido; expectedVersion positivo.
- Estados PendingActivation,Active,Suspended. Invitación aleatoria de alta entropía, hash, un uso, 15 minutos.
- Reenvío invalida anterior. Activación sin login automático.
- Suspensión/cambio de rol revocan sesiones e invitaciones en una transacción.
- Reactivación exige login nuevo y no restaura sesiones.
- active=true no activa pendientes: 409 ACCOUNT_TRANSITION_INVALID.
- Reenvío solo PendingActivation. Registro público crea exclusivamente Patient.

## API propuesta /api/v1
- GET /auth/me conserva userId,email,role y añade permissions[],accountVersion.
- GET /admin/staff: after UUID,limit predeterminado20/rango1–100,role,status; lista paginada - mínima, excluye pacientes/administradores.
- POST /admin/staff/invitations {email,role}:201 cuenta pendiente sin token.- 
- POST /admin/staff/{id}/invitation {expectedVersion}: reenvío/nueva versión sin token.
- POST /auth/staff/activate {token,password}: activación sin sesión.
- PATCH /admin/staff/{id}/role {role,expectedVersion}: cuenta/nueva versión.
- PATCH /admin/staff/{id}/active {active,expectedVersion}: cuenta/nueva versión.
- POST /admin/staff/{id}/revoke-sessions {expectedVersion}: confirmación/nueva versión.
- GET /admin/staff/audit-events: paginación y solo metadatos saneados de personal.
- Mantener ApiResponse<T>/ErrorApiResponse. Errores 400/401/403/404/409/429 con code.
- Duplicado observable solo para administrador autorizado; neutralidad pública de login/recuperación.
- Conservar límites auth existentes y extender protección activación/reenvío; documentar política numérica.

## UX
- Portal: listado → selección → confirmación → API confirmada → actualización.
- 409 conserva contexto y permite recargar; nunca sobrescribe silenciosamente.
- Pantalla de activación sin sesión; token sin persistencia/capturas/logs/URL.
- 401 limpia sesión/estado sensible y reautentica; mensaje “La sesión ya no está disponible”.
- 403 comunica falta de permiso y evita bucle login.
- Flutter exclusivamente paciente, sin nueva persistencia de tokens.

## Criterios Given/When/Then
1. Cada rol consulta identidad efectiva; acción no concedida devuelve403 sin datos.
2. Paciente/personal no administrador no se autoeleva mediante campos o llamadas directas.
3. Invitación de rol permitido crea pendiente/auditoría y no devuelve secretos.
4. Invitación válida activa exactamente una vez; vencida/reenvío/consumida falla.
5. Suspensión invalida todas las sesiones; accesos/refresh posteriores fallan.
6. Reactivación exige login nuevo; tokens anteriores siguen rechazados.
7. Cambio de rol revoca sesiones/invitaciones y exige autenticación posterior.
8. Login/refresh concurrentes no eluden suspensión/revocación.
9. Dos cambios con igual versión: uno confirma y otro409 sin efectos parciales.
10. Fallo auditoría revierte mutación; eventos sin secretos/texto clínico.
11. Administrador no opera pacientes/Administrator/propia cuenta por las nuevas rutas.
12. Regresión Patient/Admission mantiene comportamiento y separación.
13. Fuera Development seed/invitación se rechazan antes de crear cuentas/entregar tokens.
14. Flutter/portal distinguen401/403; Playwright prueba acceso directo protegido.

## Responsables y entrega
- API: backend_mobile único responsable en api/src/.
- Flutter: coordinación ambos colaboradores en app/src/.
- Portal: portalweb/src/. Contrato API antes de integrar clientes.
- Implementer propone único patch; Validator revisa después; integración mediante PR/pruebas.
- Aprobación explícita no autoriza despliegue ni afirma producción lista.
