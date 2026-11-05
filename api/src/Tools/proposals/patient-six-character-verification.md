# Propuesta M01: código de verificación de paciente

Estado: parche aprobado por el usuario y aplicado. Validación de integración completada.

## Comportamiento

- Código aleatorio criptográfico de seis caracteres A–Z/0–9, sin distinguir mayúsculas al verificar.
- POST /api/v1/auth/verify requiere email y token. Flutter y fixtures actualizados juntos.
- Hash asociado a usuario y vencimiento; caduca a los 15 minutos, es de un solo uso y se invalida tras cinco fallos persistidos. Se conserva el rate limit existente y el intervalo mínimo de reenvío.
- El reenvío sustituye el código anterior. Recuperación de contraseña y sesiones conservan tokens largos. El MFA del portal conserva TOTP.
- Migración 004 idempotente: agrega intentos e invalida verificaciones antiguas pendientes; los pacientes afectados deben solicitar reenvío. No verifica cuentas automáticamente.

## Validación

Incluye pruebas propuestas de formato, separación por cuenta, mayúsculas/minúsculas, uso único, límite de intentos y actualización de las pruebas SMTP, reinicio y Playwright.

La compilación y las pruebas de ejecución están pendientes de integración revisada. No se iniciaron servicios ni se enviaron correos reales para preparar esta propuesta.

## Orden de integración

Revisar el parche conjunto API/Flutter/fixtures. Compilar, aplicar migración 004 con --migrate y ejecutar pruebas en base sintética con SMTP local antes de iniciar el API actualizado. No ejecutar clientes antiguos contra el contrato nuevo.

## Ajuste solicitado: modelos API response

Se reutilizan las adaptaciones de `api/api-response-models-template` existentes en `Telemedicina.Common.Models.ApiResponses`, evitando duplicar clases y conservar los errores de sintaxis/tipos de la plantilla original.

- `ApiResponse<T>` para respuestas de datos y aceptación de registro/reenvío/recuperación.
- `ErrorApiResponse` para errores de controladores, autenticación, validación automática, excepciones y rechazos sin cuerpo de `/api/v1`.
- Metadatos opcionales `Code` y `TraceId` conservan la compatibilidad del contrato existente.
- No se convierte un listado en paginado sin contrato de paginación; `PaginatedApiResponse<T>` permanece disponible para operaciones realmente paginadas.
- Salud ya usa `ApiResponse<HealthCheckResponse>`. El sondeo de infraestructura `/health` queda fuera del contrato REST.
- Se agregan comprobaciones propuestas de serialización y de respuestas HTTP. Verificada únicamente la sintaxis Python de los fixtures reconstruidos y que el parche aplica limpiamente; compilación y ejecución siguen pendientes.

La propuesta conjunta reemplazó la versión anterior del mismo parche y fue aplicada tras la aprobación explícita del usuario.

## Integración aprobada

Aplicado tras la instrucción explícita «aplica el parche». La compilación detectó una sobrecarga incompatible en `UseStatusCodePages`; se corrigió tipando `StatusCodeContext` y proporcionando opciones de serialización explícitas.

Pruebas .NET: 12 aprobadas. Flutter: análisis sin incidencias y seis pruebas aprobadas, incluyendo envío del correo y código normalizado. El fixture HTTP ajusta su ritmo para respetar el límite real de autenticación. El primer intento de integración encontró HTTP 429 por exceso de solicitudes de la propia suite; no se relajó la protección del servidor.

## Resultado final

PASS: 12 pruebas .NET, 14 HTTP, una SMTP local, siete Playwright y seis Flutter; análisis Flutter sin incidencias. La migración 004 se aplicó correctamente a la base configurada. Los tokens anteriores pendientes requieren reenvío. Evidencia: `hitos/api/primer-hito-api/validaciones/02-verificacion-seis-caracteres_completado.md`.
