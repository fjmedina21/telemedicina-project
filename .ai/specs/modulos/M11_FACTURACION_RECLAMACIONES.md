# M11 — Facturación, pagos y reclamaciones

**Prioridad:** P1 automatización; P0 registro mínimo y conciliación manual si el piloto cobra · **Actores:** paciente, admisión, facturación, ARS · **Depende de:** M04, M05, M08

## Objetivo
Registrar cargos y pagos sin acoplar el cierre clínico a estados económicos, y preparar futuras reclamaciones a aseguradoras.

## Funciones e historias
- US-41: Como paciente, conozco importe estimado/copago y método de pago antes de confirmar cuando aplique.
- US-42: Como facturación, registro pagos/cargos manuales autorizados y emito comprobante adecuado.
- US-43 (P1): Como paciente, pago con pasarela y consulto estado sin exponer datos de tarjeta a la aplicación.
- US-44 (P1): Como facturación, genero y gestiono reclamaciones frente a ARS.

## Pantallas Flutter
Resumen de costo; estado de pago; comprobante; historial de pagos (P1).

## Reglas
- Cotización distingue precio, cobertura aprobada, copago y total propio; nunca prometer monto final sin datos contractuales.
- `Payment` independiente de `Appointment` y `Encounter`.
- Reintentos de cobro con idempotencia y conciliación del proveedor; Flutter no almacena PAN/CVV.
- Reembolso y controversia no borran la evidencia de transacción.
- La generación de factura o comprobante fiscal debe validarse con reglas locales antes de producción.

## Datos mínimos
`PriceQuote(id, appointmentId, currency, gross, coverageEstimate?, patientEstimate?)`; `Invoice`; `Payment(id, invoiceId, amount, status, gatewayRef?)`; `Claim(id, insurerId, appointmentId, status, reference?)`.

## API candidata
`GET /appointments/{id}/quote`, `GET /invoices/me`, `POST /payments/intents` (P1), `GET /payments/{id}`; endpoints de conciliación y reclamaciones protegidos.

## Excepciones
Error pasarela, monto cambia, pago duplicado, cobertura retroactivamente rechazada, devolución, servicio cancelado.

## Criterios de aceptación
1. Dos confirmaciones del mismo intento no registran dos pagos.
2. Un fallo de pago se comunica sin afectar historia clínica ya creada.
3. Comprobantes y transacciones solo los consultan usuarios autorizados.
4. Si no hay pasarela, el proceso manual queda auditado y explícitamente marcado como manual.
