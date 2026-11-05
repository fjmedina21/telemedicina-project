# Segundo hito Flutter — alcance de trabajo

Plan coordinado con [el plan transversal de API](../../api/segundo-hito-api/PLAN.md). Estado: propuesta, no implementación.

## Objetivo

Conservar una experiencia de acceso coherente para el paciente mientras la API incorpora roles y permisos explícitos. La app solo representa capacidades autorizadas; la autorización real siempre ocurre en el servidor.

## Incluido

- Adaptar el modelo de sesión y la respuesta de identidad a los contratos aprobados.
- Manejar sesión expirada, cuenta suspendida, sesión revocada y falta de permiso con mensajes comprensibles y sin información sensible.
- Mantener registro, verificación, recuperación, perfil y funciones existentes del paciente.
- No mostrar opciones de personal ni permitir elegir o modificar roles desde Flutter.
- Evitar almacenamiento de tokens en texto plano y limpiar la sesión local ante logout o revocación detectada.
- Usar exclusivamente cuentas y datos sintéticos en desarrollo.

## Fuera de alcance

Tutor/dependientes, agenda clínica del profesional, expediente, triaje, consentimiento, video, recetas y documentos clínicos. No se habilitan roles distintos del paciente en la app sin decisión de producto.

## Criterios de aceptación

1. El login y las funciones actuales del paciente siguen funcionando con el contrato de identidad actualizado.
2. Una sesión inválida o revocada no permite seguir consultando recursos protegidos.
3. Un error de permiso no expone datos ni queda presentado como error genérico de red.
4. La app no confía en roles enviados por el cliente ni ofrece controles para asignarlos.
5. Estados de carga, error, sesión expirada y reautenticación son accesibles y están localizados.
6. Cambios de contrato se integran después de aprobar OpenAPI y se revisan por PR.

## Handoff y dependencia

Esperar el contrato de API aprobado antes de integrar. Responsable: área frontend `app/src/`, con coordinación de ambos colaboradores Flutter. Validación funcional y de dispositivos queda en el plan de ejecución; no se afirma aquí que se haya realizado.
