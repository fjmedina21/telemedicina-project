# Retirada de pruebas de autenticación obsoletas
Solicitud del usuario: «elimina los test de mfa ya que se elimino».
Alcance: corrección de propuesta H02-SPEC-v1 ya aprobada; no cambia la spec ni aplica código.

Inventario de suites activas API, portal y Flutter:
- milestone_e2e.py: escenario test_08_staff_mfa_required obsoleto, helper otp(secret) sin usos,
  imports base64/hashlib/hmac/struct exclusivos del helper.
- portalweb/src/tests/browser/support/totp.ts: helper sin consumidores.
- No se encontraron escenarios equivalentes en la suite Flutter.

Propuesta actual:
- Retira escenario del segundo factor y conserva prueba de login por contraseña válido,
  consulta de identidad, logout y rechazo de contraseña incorrecta.
- Retira helper Python y sus imports exclusivos. No elimina base64 de otros runners:
  allí participa en la clave del correo y no corresponde a esta solicitud.
- Retira implementación/exports del helper de portal; queda únicamente comentario de retirada.
  El archivo no se borra porque WorkspaceTools prohíbe deleted file mode.
- No modifica código productivo, pruebas de permisos/sesiones, verificación de registro,
  snapshots históricos de propuestas ni documentación clínica.

Verificación: inventario textual y parseo Python de la propuesta; verify_patch valida rutas
y git apply --check --whitespace=error. No se ejecutaron pruebas funcionales.
El parche anterior SHA256 361ad5f2e493beb26bb7e79f768fabeb59d668d48301ddb2023b08bb6a821555
queda sustituido por una única propuesta actualizada. Los informes anteriores solo validan
su hash correspondiente. Esta corrección queda pendiente de re-revisión de Validator.
