# Identidad visual compartida — Telemedicina

Estado: diseño aprobado por el usuario e integrado en las fuentes activas el 2026-11-05.

## Dirección visual

Identidad serena y funcional con verde profundo, fondos claros, tipografía del sistema y un símbolo blanco en un cuadrado verde. Se conserva el nombre del proyecto. La interfaz prioriza las tareas de pacientes y admisión; no presenta video, documentos ni atención clínica como capacidades disponibles.

La fuente de referencia de los valores es `brand-tokens.json`. Flutter los implementa en `BrandTheme`; el portal en las variables de `globals.css`. Los componentes se adaptan a la densidad de cada plataforma conservando colores, jerarquía y significado de estados. No se agregan dependencias ni servicios de tipografía.

## App Flutter

- Marca visible en la cabecera, aviso discreto de demostración y contenido centrado con ancho acotado.
- Acceso, registro, verificación y recuperación con título contextual, campos consistentes, autofill y opción de mostrar contraseña.
- Verificación conserva el código alfanumérico de seis caracteres y su contrato aprobado.
- Perfil agrupado en datos personales, contacto de emergencia e información autodeclarada.
- Citas con estados en español, separando cita y cobertura. Acciones y textos se distribuyen verticalmente para evitar desbordamientos.
- Estados vacíos útiles y avisos legibles. Se conserva la navegación inferior y todos los flujos existentes.

## Portal web

- Navegación lateral en escritorio y navegación horizontal desplazable en móvil, con ubicación activa identificada.
- Acceso con composición de dos columnas que se convierte en una sola en pantallas pequeñas.
- Cabeceras claras y resúmenes calculados exclusivamente sobre las citas cargadas.
- Tabla de admisión en escritorio y tarjetas en móvil, con búsqueda local, filtro por estado y lectura diferenciada de paciente/servicio/profesional.
- Revisión manual con foco inicial en la evidencia y jerarquía de acciones. Se conservan las reglas y advertencias de cobertura.
- Formularios de catálogo, agenda y auditoría heredan controles, espacios y colores. Se corrige el alcance de Tailwind para incluir `features` y `shared`.

## Accesibilidad y revisión

Contrastes calculados: botón principal 7,78:1; texto secundario 5,36:1; límites de campos 3,18:1 sobre blanco. Estados con etiquetas, foco visible, enlace para saltar al contenido y controles de al menos 44px en web y 48px en app. No se afirma conformidad integral WCAG: requiere auditoría completa del producto.

`review_design.py` construye copias temporales de ambas aplicaciones; nunca aplica el diseño al código activo. Compila el portal, analiza y prueba Flutter, compila Flutter web para la vista previa y ejecuta los flujos Playwright con PostgreSQL efímero. Los servidores y el contenedor se limpian al terminar. No carga `.env`, no envía correo real y usa datos sintéticos.

Las capturas de acceso muestran las interfaces propuestas reales. Las capturas del portal con citas usan respuestas simuladas únicamente para ilustrar su composición y comprobar filtros; las pruebas de integración separadas sí utilizan la API y PostgreSQL temporales.

El usuario revisó la propuesta y aprobó explícitamente su integración conforme a AGENTS.md. No se alteran API, migraciones, permisos ni decisiones clínicas.

## Artefactos de integración

Parche conjunto: `hitos/portalweb/primer-hito-portalweb/propuestas/identidad-compartida.patch`.
Aplicado una sola vez desde la raíz tras aprobación explícita; incluye app y portal. Se conserva como evidencia de revisión.
Los archivos JSON de implementación y el ejecutor aislado permanecen en los directorios `src/Tools/proposals/minimal-brand` de cada componente.
