# Recap — Primer hito usable y correo

Fecha: 2026-11-05.

Este documento resume el trabajo y las verificaciones registrados en la conversación. No representa una nueva ejecución de las pruebas ni una aprobación de producción.

## Alcance y decisiones

- Proyecto compuesto por API ASP.NET Core, app Flutter para pacientes y portal web de admisión/administración.
- Continuar con SDK y runtime .NET 10; el runtime 9 instalado no es el objetivo del proyecto.
- Organizar los entregables por componente en `hitos/api/primer-hito-api`, `hitos/app` y `hitos/portalweb`, siguiendo la estructura existente.
- Usar Playwright para las pruebas que necesitan navegador.
- Revisar propuestas antes de integrar cambios. No se autorizó publicar cambios, hacer push ni crear un PR en el repositorio público.
- Mantener secretos exclusivamente en configuración privada y usar datos sintéticos en las pruebas.

## Trabajo realizado

### API y primer hito

- Se trabajó la configuración de `.gitignore` y del entorno local.
- Se implementó el primer incremento de autenticación, verificación de correo, recuperación de acceso, sesiones de admisión, perfil de paciente, catálogo, disponibilidad, citas, admisión, auditoría y notificaciones.
- Se incorporaron migraciones PostgreSQL, restricciones de concurrencia, operaciones idempotentes y procesamiento de correo mediante outbox con reintentos y deduplicación.
- Se ejecutó `--seed-synthetic` y quedó disponible una cuenta sintética de admisión, junto con catálogo y horarios de demostración. Su contraseña permanece en el `.env` privado.
- Se habilitó la documentación de la API. Un proceso antiguo mostraba solamente salud en Swagger; al reiniciar con el código actualizado se verificaron 36 rutas y 40 operaciones.
- Se reorganizó el backend por funcionalidades e infraestructura; Flutter por aplicación, funcionalidades y componentes compartidos; y el portal por funcionalidades y componentes comunes.
- Se eliminaron carpetas vacías sin borrar archivos.

### Correo Gmail y certificados

- Se configuró Gmail con `smtp.gmail.com`, puerto `587` y STARTTLS, usando las credenciales privadas proporcionadas por el usuario.
- Se identificó un fallo de comprobación de revocación de certificados en el entorno local macOS/.NET: `RevocationStatusUnknown`.
- Tras presentar un parche y recibir aprobación explícita, se agregó `Mail:CheckCertificateRevocation` con valor predeterminado `true`.
- En Development se permite configurar `false` para resolver ese fallo local. Fuera de Development la comprobación permanece obligatoria, aunque la configuración solicite desactivarla.
- Se conservan las validaciones de confianza, vigencia y nombre del certificado; no se agregó una aceptación indiscriminada de certificados.
- El `.env` privado quedó configurado para correo SMTP real y la excepción local de revocación. No se incluyen sus valores sensibles en este historial.
- Propuesta revisada: `api/src/Tools/proposals/gmail-local-revocation.patch`.

## Verificaciones registradas

- Pruebas ejecutables .NET después del cambio de correo: **9 aprobadas, 0 fallidas**, incluyendo cuatro casos de política de revocación por entorno.
- Comprobación SMTP de la API: **SMTP_AUTH_OK**.
- Registro HTTP de un paciente sintético: **202 Accepted**.
- Procesamiento del correo de verificación por el worker: estado **Sent**, en **un intento**. Esto confirma la aceptación del envío por SMTP; no se inspeccionó la bandeja de entrada.
- La prueba dejó una cuenta sintética y su registro de outbox en la base de datos; se cerró la sesión administrativa utilizada para consultar el resultado.
- En validaciones anteriores se registraron compilaciones de API y portal, análisis y cinco pruebas Flutter, doce pruebas HTTP, una prueba de fixture SMTP y siete pruebas Playwright. Estos resultados corresponden a sus respectivas etapas y no deben interpretarse como una nueva corrida completa tras el último parche.

## Acceso local cuando se vuelvan a iniciar los servicios

| Servicio | Dirección |
| --- | --- |
| API / salud | `http://localhost:5000/api/v1/health` |
| Swagger | `http://localhost:5000/swagger` |
| OpenAPI | `http://localhost:5000/openapi/v1.json` |
| Portal web | `http://localhost:3000` |

El portal de admisión utiliza correo y contraseña. La verificación de registro de pacientes usa un código alfanumérico de seis caracteres.

Comandos utilizados para iniciar los componentes:

```sh
# Desde la raíz del proyecto
python3 api/src/Tools/run_local.py --configuration Release --no-build

# Desde app/src
flutter run -d macos --dart-define=API_BASE_URL=http://127.0.0.1:5000/api/v1

# Desde portalweb/src
npm run dev
```

El comando de API con `--no-build` requiere una compilación Release vigente.


## Pendientes y límites

- Decisiones operativas, revisión PR/UAT/pipeline remoto y validación del correo en producción quedan aplazadas por instrucción del usuario. Android/iOS fue retirado de los pendientes.
- No se publicaron cambios ni se ejecutó el pipeline remoto.
- Video, documentación clínica y pagos siguen fuera del primer hito implementado.

## Ajuste de alcance posterior

- Pendientes activos: actualizar documentación/evidencias y consolidar parches/manifiestos.
- Nueva solicitud: verificación de registro de paciente con código alfanumérico de seis caracteres.
- Parche `api/src/Tools/proposals/patient-six-character-verification.patch` aprobado y aplicado por instrucción explícita del usuario. Incluye API, Flutter, migración y adaptación de pruebas.
- Migración 004 ejecutada correctamente en la base configurada; los tokens anteriores pendientes requieren reenvío.
- Contrato de verificación: correo y código de seis caracteres, vigencia de 15 minutos, un solo uso y cinco intentos fallidos. Respuestas basadas en ApiResponse<T>/ErrorApiResponse.

- Validación final del cambio: 12 pruebas .NET, 14 HTTP, una SMTP local, siete Playwright y seis Flutter aprobadas; análisis Flutter sin incidencias. Evidencia en `hitos/api/primer-hito-api/validaciones/02-verificacion-seis-caracteres_completado.md`. Los servicios de pruebas se detuvieron al terminar.

## Identidad visual compartida

El usuario aprobó el diseño minimalista compartido y se integró el parche `hitos/portalweb/primer-hito-portalweb/propuestas/identidad-compartida.patch` en app y portal. Incluye tema de marca, navegación adaptable, formularios consistentes, estados en español, filtros de citas y tarjetas móviles. Los 25 archivos integrados coinciden con la versión previamente compilada y validada con ocho pruebas Flutter y siete Playwright. Comprobación de formato sin errores. Las evidencias y los informes se encuentran en los hitos de app y portal. No se iniciaron servicios durante esta integración.

## Cuentas sintéticas

- La cuenta de admisión existente conserva su correo y contraseña en `api/src/.env`. Los roles de autenticación disponibles son Patient y Admission; tutor, médico, soporte y administrador aún no son roles admitidos por la base/API del hito. El profesional de demostración del catálogo no es una cuenta de usuario.
- API, portal y app Flutter permanecen detenidos por solicitud del usuario.
