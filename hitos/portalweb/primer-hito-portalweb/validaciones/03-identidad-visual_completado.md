# Validación de identidad visual compartida

Fecha: 2026-11-05. Estado: diseño aprobado e integrado; validado previamente en copias aisladas.

## Resultado

PASS para las comprobaciones ejecutadas. El usuario aprobó la integración y el parche se aplicó correctamente.

| Comprobación | Resultado |
| --- | --- |
| Portal Next.js: build de producción y comprobación de tipos | Aprobada |
| Flutter analyze | Sin incidencias |
| Flutter test | 8 aprobadas |
| Flutter build web para vista previa | Aprobada |
| Playwright Chromium contra portal compilado, API y PostgreSQL temporal | 7 aprobadas |
| Búsqueda y filtro de citas en la vista previa | Aprobados |
| Portal a 390px: sin desbordamiento horizontal de página | Aprobado |
| Acceso y navegación de Flutter a Mis citas con API simulada | Aprobados |
| Revisión visual de capturas de acceso, escritorio y móvil | Realizada |
| git apply --check del parche conjunto | Aprobado |
| Hashes de fuentes activas y propuesta | Los 25 archivos integrados coinciden con los hashes propuestos del manifiesto |

Se corrigieron durante la revisión una superficie Material que ocultaba los efectos de interacción, la cobertura de Tailwind sobre features/shared y la presentación de la tabla en móvil, sustituida por tarjetas.

El origen de la API temporal se configuró para localhost:5096. La captura Flutter usa roles accesibles tab/group, tal como los expone el framework. Estos ajustes pertenecen al ejecutor de revisión y no cambian reglas del backend.

Las pruebas de integración usan cuentas sintéticas y PostgreSQL efímero. Las capturas de trabajo usan respuestas simuladas con datos sintéticos para mostrar citas de manera estable. No se carga el .env privado, no se modifica la base remota y no se envía correo real. Todos los servidores temporales se detienen al finalizar; el contenedor de pruebas se elimina.

## Artefactos

- Parche conjunto: hitos/portalweb/primer-hito-portalweb/propuestas/identidad-compartida.patch.
- Manifiesto y valores de marca junto al parche.
- Capturas en evidencias/diseno-minimalista de cada componente.
- Reporte Playwright detallado: portalweb/src/Tools/proposals/minimal-brand/browser-results/playwright-results.json.
- Ejecutor reproducible y contenido propuesto en src/Tools/proposals/minimal-brand de cada componente.

No se afirma conformidad integral de accesibilidad ni validación nativa Android/iOS; esta última fue retirada del alcance por el usuario. Las capturas de la app se generaron desde Flutter web con viewport móvil.

## Comprobación posterior a la integración

`git apply --check` y aplicación del parche: salida 0. Los 25 archivos integrados coinciden byte a byte con la versión que pasó compilación, ocho pruebas Flutter y siete Playwright en copias aisladas. `git diff --check` no presenta errores en las fuentes afectadas. No se repitieron las pruebas de ejecución: la implementación integrada es idéntica a la validada. Los servicios no se iniciaron durante la integración.
