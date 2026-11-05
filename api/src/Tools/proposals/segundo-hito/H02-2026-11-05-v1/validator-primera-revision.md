# Validator — primera revisión H02
Estado: CHANGES_REQUIRED. Revisión estática; ninguna prueba funcional ejecutada.
Parche revisado inicialmente:
SHA256 3397604e957807782e0334751f5ff768df5baf5a556d2920c1034ce823e42371
Tamaño121159 bytes;43 archivos.

1. P1 — SessionHandler.Reject eliminaba tm_refresh ante cualquier401.
   Rompía renovación del portal al caducar access aunque refresh siguiera válido.
   También debía revisarse la eliminación en SessionRejectedException401.
2. P2 — PatientController.load añadió /auth/me como Map pero el fixture de
   notifications_view_test.dart devolvía por defecto data=[] y rompería la prueba de avisos.
3. P2 — StaffActiveRequest bool Active aceptaba JSON sin active como false,
   pudiendo suspender sin intención explícita. Debe exigirse presencia y400 sin mutación.

La propuesta actual incorpora ajustes para los tres puntos y nuevas pruebas propuestas.
Estado de esos ajustes: pendiente de re-revisión de Validator; no se afirma resolución
ni ejecución de las pruebas.
