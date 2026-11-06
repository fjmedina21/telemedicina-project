# Spec revisable: dataset realista sin citas

**Versión:** `DATA-RESET-REALISTIC-v2`  
**Estado:** pendiente de aprobación humana.  
**Base:** `api/src/DEMO_DATA_RESET.md`, dataset ficticio `SEED-REALISTA-v1`.  
**Objetivo:** reemplazar los datos de desarrollo por el dataset realista ficticio, sin citas creadas.

## Alcance

- Actualizar `DemoDataPlan` y `DemoDataReset` para que este dataset y futuros resets no creen citas.
- Reemplazar los datos del esquema `telemedicina_m1` en la base de desarrollo identificada y aprobada.
- Conservar las migraciones 001–008, `schema_versions`, los demás esquemas y las fixtures sintéticas aisladas de pruebas.
- No eliminar seeders ni migraciones.

## Datos finales previstos

| Entidad | Cantidad prevista |
|---|---:|
| Cuentas ficticias | 30 |
| Perfiles ficticios | 24 |
| Servicios de demostración | 6 |
| Profesionales de catálogo de demostración | 6 |
| Horarios | 800 |
| Horarios bloqueados | 60 |
| Preferencias de correo | 24, desactivadas |
| Citas | **0** |
| Historial de citas | **0** |
| Notificaciones de cita | **0** |

Las direcciones de correo `.invalid`, perfiles, teléfonos y entidades del dataset son ficticios. No representan personas, profesionales, pólizas ni servicios habilitados reales.

## Cambios requeridos en la receta

- Quitar la creación y distribución de objetos `DemoAppointment`.
- Omitir la inserción de citas y las notificaciones asociadas.
- Ajustar `Expected()`, `AssertIntegrity()` y el marcador de auditoría para comprobar cero citas, historial y notificaciones.
- Mantener las tablas, restricciones y trigger de historial del esquema; sin insertar citas, no se generan filas de historial.

## Operación de base de datos

El preflight anterior devolvió `DEMO_DEVELOPMENT_ONLY` antes de abrir conexión; no modificó datos ni identificó la base.

Tras aprobar esta versión:

1. Aplicar el cambio de receta y las comprobaciones correspondientes, conservando migraciones y fixtures de prueba.
2. Detener API y clientes antes del preflight operativo.
3. Identificar de forma saneada el host y nombre de la base destino; no imprimir credenciales ni datos personales.
4. Exigir `ASPNETCORE_ENVIRONMENT=Development`, `Milestone:Enabled=true`, `Mail:Mode=development` y una base local reconocida. Abortar si el destino es desconocido o de producción.
5. Ejecutar `--reset-demo-data` sin `--execute`; verificar el esquema y presentar el destino y conteos actuales/previstos.
6. Solicitar aprobación explícita del reporte de dry-run antes de ejecutar `--reset-demo-data --execute`.
7. Verificar y guardar conteos posteriores sin información personal.

El reset reemplaza el contenido de 17 tablas de `telemedicina_m1`, incluidas cuentas, perfiles, sesiones, auditoría, historial, citas y notificaciones. No tiene respaldo automático ni reversión posterior al commit. Conserva `schema_versions`, las migraciones 001–008 y otros esquemas. Las guardas de manifiesto, versiones, triggers, bloqueos y referencias externas deben pasar; ante cualquier discrepancia se aborta sin fallback destructivo.

## Criterios de aceptación

- El dry-run no modifica datos e identifica el destino de forma saneada.
- Los conteos previstos y posteriores son los indicados arriba, especialmente cero citas, historial y notificaciones de cita.
- La receta revisada no genera citas en ejecuciones futuras.
- Migraciones, `schema_versions`, otros esquemas, cuentas ficticias y oferta de demostración se conservan según los conteos previstos.
- Cualquier configuración insegura, base desconocida, manifest/version mismatch o trigger inesperado cancela el proceso.
- El reporte final registra el resultado del commit y conteos; omite correos, contraseñas, conexiones y datos personales.

## Aprobación requerida

La aprobación de `DATA-RESET-REALISTIC-v2` autoriza preparar la actualización de la receta y ejecutar únicamente el preflight de solo lectura. **No autoriza todavía el reemplazo irreversible.** Para ese paso se requiere aprobación posterior del reporte con el nombre de la base y los conteos actuales/previstos.
