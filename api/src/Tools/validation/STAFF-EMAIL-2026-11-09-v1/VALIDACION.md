# Validación local — invitación de personal por correo
Fecha: 2026-11-09. Rama: feat/staff-invitation-email.
Ejecución: STAFF-EMAIL-2026-11-09-v1. Spec: STAFF-INVITATION-EMAIL-SPEC-v1.

## Resultado: PASS local
El usuario aprobó la spec y luego autorizó explícitamente aplicar el parche con el mensaje "autorizo aplicar el parche".
Se aplicó proposal.patch SHA256 bd0500ea661e2a21da93d539cbc20df19fed72d0b4d28cfdedefffad0057bada.
API y portal encolan la invitación mediante el correo existente; se permite personal pendiente, se invalida correo anterior y se protege el token.
El parche permanece sin commit ni integración en main.

## Verificaciones ejecutadas
| Comprobación | Resultado |
|---|---|
| dotnet build --configuration Release api/src/Telemedicina.Api/Telemedicina.Api.csproj | PASS, 0 advertencias y 0 errores |
| npm run build en portalweb/src | PASS, compilación y tipos correctos |
| dotnet run --configuration Release --project api/src/Telemedicina.Tests/Telemedicina.Tests.csproj | PASS, 46/46 |
| SMTP local, smtp_fixture_test.py --staff-invitations | PASS |
| Regresión second_milestone_e2e.py | PASS, 8/8 en 184.020 segundos |
| Mensaje invalidado tras obtener lease y antes de enviar | PASS, invalidación mediante SQL controlado bajo bloqueo compartido |
| Caducidad de invitación exacta con outbox aún vigente | PASS |
| Correo ordinario de cuenta inactiva | PASS, Superseded |
| git apply --reverse --check proposal.patch | PASS, código coincide con parche autorizado |
| git diff --check | PASS |

La prueba SMTP comprobó recepción para cuenta pendiente, reintento temporal, deduplicación, enlace sin token en URL, activación de un uso, mailbox404 en SMTP, cooldown429 y denegación403 al personal.
H02 verificó permisos, activación, suspensión/reactivación, cambio de rol, revocación, versiones concurrentes, auditoría y regresión de paciente.

## Entorno y evidencia
PostgreSQL 17 temporal desde imagen Docker ya disponible; almacenamiento efímero, puerto loopback y credenciales sintéticas generadas por ejecución.
El runner no lee api/src/.env, no usa la base remota ni envía correo real.
SMTP escucha exclusivamente en loopback. El contenedor se eliminó al terminar.
- integration-results.json: resultado consolidado final PASS y limpieza.
- smtp_staff_invitations.log: prueba SMTP local PASS, ejecutada en el primer intento.
- h02_regression.log: 8 pruebas OK.
- first-attempt.json y second-attempt.json: historial de fallo del runner.
El fallo adicional se debió a que el runner reencolaba mensajes conservando available_at futuro. Se corrigió solo el runner (available_at=now()) y se repitieron los casos fallidos y H02 en otra base temporal. La prueba SMTP ya aprobada no se repitió; el resultado final referencia su log conservado.
- run_validation.py: reproduce pruebas con una base temporal nueva; usa Python con smtpd, como el fixture existente. Sin --skip-smtp ejecuta también SMTP.

## Dictamen actualizado Validator
PASS para validación local; sin hallazgos bloqueantes.
Validator revisó logs y resultados. Builds y unitarios fueron ejecutados por el orquestador.
El caso de lease usa invalidación SQL controlada, no representa una carrera HTTP completa.
Verificaciones adicionales: rollback ante fallos de origen/cifrado/auditoría y carreras HTTP completas del worker frente a operaciones de invitación.
La validación local no equivale a UAT ni producción.

## Uso
El envío real requiere API iniciada con Mail__Mode=smtp y configuración SMTP privada válida.
El arranque local conserva Mail__Mode=development por defecto, que captura mensajes.
Tras cambiar modo es necesario reiniciar la API y emitir o reenviar una invitación; correos anteriores permanecen asociados a su canal.
