# Reporte de validación H02 — propuesta

## Veredicto provisional: PASS de revisión estática

Fecha: 2026-11-05. Ejecución: H02-2026-11-05-v1. Spec: H02-SPEC-v1.
Parche revisado: segundo-hito-propuesto.patch.
SHA256: 0a052b7fdd70e3b93004e3fea61b32910f3ebf5e54428f86df8bfccad3f1e972.
Tamaño informado en verificacion-parche.json: 126027 bytes; 45 archivos.

Este PASS es consultivo y se limita al diseño y código propuestos por inspección.
No certifica compilación, funcionamiento, seguridad de producción ni cierre del hito.
No autoriza aplicar el parche, integrar ni desplegar.

## Primera revisión y correcciones

El primer pase fue CHANGES_REQUIRED sobre el SHA256
3397604e957807782e0334751f5ff768df5baf5a556d2920c1034ce823e42371.
Los tres hallazgos quedaron atendidos en la propuesta revisada:

1. P1, renovación de cookies: SessionHandler y el manejador SessionRejectedException
   conservan las cookies ante challenges de recursos protegidos. AuthController.Failure
   elimina el par únicamente cuando falla /auth/refresh; logout conserva su limpieza
   explícita. Así se permite intentar renovación ante acceso ausente o vencido.
   El nuevo caso Playwright propone comprobar expiración real de la cookie de acceso,
   refresh exitoso y permanencia en /personal; después comprueba replay de familia
   revocada, refresh401, limpieza de cookies y retorno a login.
2. P2, regresión del fixture Flutter: notifications_view_test.dart incluye /auth/me
   con un Map de identidad Patient y campos aditivos. Se conservan las aserciones
   existentes sobre preferencias de avisos y marcado como leído.
3. P2, campo active omitido: StaffActiveRequest aplica JsonRequired a la propiedad
   Active. El caso HTTP propuesto envía solo expectedVersion y exige400, sesión
   todavía válida y accountVersion sin cambios antes de probar suspensión explícita.

Estas conclusiones proceden de inspección; las pruebas nuevas no fueron ejecutadas.
No se identificaron otros defectos bloqueantes en este pase estático.

## Matriz de criterios / evidencia / faltantes

| Criterios H02-SPEC-v1 | Evidencia propuesta | Verificación pendiente |
|---|---|---|
| 1–2: identidad y denegación / autoelevación | AccessCatalog cerrado, roles en servidor, /auth/me aditivo y matriz HTTP | Compilación y matriz real por endpoint |
| 3–4: alta e invitación de un uso | StaffController, StaffInvitations, StaffActivationController; Development y buzón privado; hash y15min | Ejecución positiva/negativa, vencimiento y reenvío |
| 5–7: suspensión, reactivación y cambio de rol | Mutación transaccional, revocación de sesiones/invitaciones y versiones; guardia compartida | Pruebas HTTP y renovación con cookies |
| 8–9: carreras y versiones | Orden de bloqueos usuario antes de sesión/invitación, relectura y expectedVersion; escenarios concurrentes propuestos | PostgreSQL con concurrencia dirigida y carga |
| 10: auditoría y rollback | Auditoría dentro de transacción y eventos saneados, resultado explícito; rechazos administrativos | Inyección de fallo de auditoría y comprobación rollback |
| 11: límites administrativos | Exclusión de Patient/Administrator como destinatarios y autoadministración denegada | Probar otro administrador como destinatario y revisar negativas |
| 12: regresión Patient/Admission | Capacidades existentes preservadas, rutas compartidas limitadas explícitamente, fixture Flutter corregido | Regresión completa del primer hito |
| 13: entorno sintético | Guardas Development + Milestone:Enabled en rutas/seed; guardas unitarias propuestas | Ejecución de guardas y migración real |
| 14: clientes y401/403 | Portal por capacidades, pantallas de personal/activación, Flutter Patient, limpieza de estado y casos nuevos | Build TypeScript/.NET/Flutter, widget y Playwright reales |

## Revisión de seguridad, datos y concurrencia

La migración005 sigue el patrón de schema_versions y no reescribe migraciones
históricas. El catálogo no concede al administrador capacidades de admisión ni acceso
clínico. Practitioner/Support se limitan a identidad y sesión propias. El registro
público conserva Patient. La nueva gestión administrativa no opera pacientes ni
administradores. Los destinatarios y las versiones se verifican dentro de transacción.

Las invitaciones se proponen exclusivamente para Development, con hash en su tabla
y token claro únicamente en el buzón privado existente protegido por clave. No se
incorporan al outbox SMTP ni a respuestas administrativas, URLs o auditoría.
La pantalla de activación mantiene el secreto en memoria y campo enmascarado.
El bootstrap usa configuración privada y no cambia credenciales/roles existentes.

Las escrituras protegidas revalidan al actor bajo bloqueo; administración ordena
bloqueos actor/destinatario. Refresh bloquea usuario antes de volver a consultar y
bloquear sesión. La revisión no detectó una inversión evidente de ese orden, pero
no pueden acreditarse la ausencia de deadlocks ni la correcta serialización sin
pruebas reales de PostgreSQL. La vigencia absoluta de sesión se conserva.

No se habilitan decisiones clínicas, expedientes, consentimiento, recetas ni datos
reales. No se emite conclusión legal o clínica.

## Evidencia disponible y pruebas pendientes

verificacion-parche.json informa PASS de validación de rutas y de
 git apply --check --whitespace=error, así como applied=false y
functionalTestsExecuted=false. Implementer informa parseo Python satisfactorio.
Son comprobaciones de artefacto, no resultados de aceptación funcional.

Pruebas propuestas: permisos/guardas en el ejecutable .NET; ocho escenarios HTTP
H02; siete casos Flutter y fixture previo de avisos adaptado; tres casos Playwright
H02; regresión del primer hito. Deben ejecutarse en un checkout autorizado con
PostgreSQL sintético y sin SMTP real. No guardar storageState, trazas, vídeos,
credenciales ni tokens como evidencia.

Además de esas suites faltan compilación .NET/TypeScript/Flutter, migración y
arranque real, OpenAPI generado, expiración/reenvío de invitación, fallo de auditoría,
segundo administrador como destinatario, accesibilidad, revisión PR y UAT.

## Compatibilidad y recomendación humana

El diff se limita a api/src/, app/src/ y portalweb/src/. /auth/me conserva sus campos
previos y añade permissions/accountVersion. Los wrappers de respuestas permanecen.
API mantiene un responsable backend_mobile; Flutter requiere coordinación de ambos
colaboradores y portal revisión cruzada del contrato antes de integrar por PR.

La propuesta corregida puede presentarse para revisión humana del parche y posterior
validación en un entorno autorizado. El hito continúa abierto hasta pruebas,
revisión y aceptación de cierre; este informe no sustituye esas puertas.

## Revisión incremental de la limpieza de pruebas solicitada por el usuario

Solicitud: «elimina los test de mfa ya que se elimino». Esta revisión incremental
corresponde al hash final indicado al inicio. Sustituye el hash
361ad5f2e493beb26bb7e79f768fabeb59d668d48301ddb2023b08bb6a821555,
que mantenía el PASS estático anterior. No se repitió la revisión integral: el delta
nuevo solo afecta a pruebas y helpers obsoletos de autenticación.

Resultado consultivo: PASS estático del delta. milestone_e2e.py retira la expectativa
del segundo factor, el helper otp y sus imports exclusivos base64/hashlib/hmac/struct.
La prueba de personal conserva login válido, identidad Admission, logout y rechazo401
de contraseña incorrecta. La modificación no debilita las pruebas de autorización,
revocación de sesiones ni concurrencia previamente revisadas.

El helper de portal tests/browser/support/totp.ts no tiene consumidores en el inventario
revisado. Su implementación, import createHmac y export se retiran; queda solo un comentario
porque el formato de propuesta permitido no admite borrar archivos. No se detectan
imports consumidores huérfanos por este cambio. El contenido final propuesto de las
pruebas afectadas no conserva referencias al mecanismo retirado. Los archivos activos
siguen intactos hasta una integración explícita del parche.

verificacion-parche.json reporta nuevamente verify_patch/--check y parseo Python
satisfactorios para el paquete actualizado. No hay compilación ni ejecución funcional.
Permanecen todos los pendientes y límites de aceptación descritos en este informe.
