# M01 — Identidad, autenticación y autorización

**Prioridad:** P0 · **Actores:** paciente, tutor, médico, admisión, soporte, administrador · **Depende de:** plataforma base

## Objetivo
Permitir acceso seguro y diferenciado a la aplicación y al portal según identidad, rol y vínculo asistencial.

## Funciones e historias
- US-01: Como paciente, puedo crear una cuenta con datos mínimos y verificación de contacto para usar la app.
- US-02: Como usuario, puedo iniciar/cerrar sesión y recuperar acceso sin exponer si una cuenta existe.
- US-03: Como profesional, accedo únicamente a las funciones del rol y pacientes asignados/autorizados.
- US-04: Como administrador, puedo suspender usuarios y revocar sesiones, con auditoría.

## Pantallas Flutter
Bienvenida/onboarding; registro; verificar contacto; iniciar sesión; recuperar contraseña; seguridad de cuenta; sesión expirada.

## Reglas y seguridad
- Identidad fuerte del personal y MFA obligatoria para personal privilegiado (en MVP puede resolverse mediante proveedor externo).
- El rol no llega confiable desde Flutter; permisos validados por API en cada lectura/escritura.
- Tokens en almacenamiento seguro del SO, rotación de refresh tokens, revocación al cerrar sesión/cambiar credenciales.
- Rate limiting y protección contra fuerza bruta; errores genéricos para evitar enumerar cuentas.
- Separar rol de tutor de un vínculo autorizado con cada dependiente.

## Datos mínimos
`User(id, email?, phone?, status, createdAt)`; `CredentialProvider`; `RoleAssignment(userId, role, organizationId?)`; `Session(id, userId, revokedAt, expiresAt)`; `ContactVerification`.

## API candidata
`POST /auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/password/forgot`, `/auth/password/reset`, `GET /auth/me`; administración en endpoints protegidos.

## Excepciones
Cuenta no verificada, bloqueo temporal, credenciales inválidas, red perdida, sesión revocada, múltiples dispositivos, OTP vencido.

## Criterios de aceptación
1. Paciente verificado accede a sus datos; otro usuario recibe `403/404` apropiado, sin filtrar existencia del registro.
2. Refresh revocado no emite nuevos tokens; cierre de sesión invalida su continuidad.
3. Doctor sin relación de atención no puede consultar un expediente aunque conozca su UUID.
4. Después de una sesión expirada, la app preserva navegación no sensible y solicita reautenticación.

## Pruebas clave
Auth positiva/negativa, abuso de endpoints, matrices de permiso, refresh concurrente, cierre de sesión, acceso a ID de otro paciente.
