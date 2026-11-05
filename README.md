# Telemedicina

API ASP.NET Core **.NET 10**, app Flutter para pacientes y portal Next.js de admisión.
El primer hito funciona en un entorno de desarrollo con datos sintéticos.

| Componente | Código | Guía |
|---|---|---|
| API y PostgreSQL | api/src/ | [API](api/src/README.md) |
| App Flutter | app/src/ | [App](app/src/README.md) |
| Portal de admisión | portalweb/src/ | [Portal](portalweb/src/README.md) |
| Specs y herramientas de agentes | .ai/ | [.ai/README.md](.ai/README.md) |
| Propuestas, parches y validaciones | hitos/ | [Hitos](hitos/README.md) |

El código se organiza por identidad, pacientes, catálogo, citas, admisión, horarios,
notificaciones y auditoría; la infraestructura y el código compartido se mantienen separados.

## Ejecutar y validar

Configurar las variables privadas de la API a partir de api/src/.env.example.
La configuración real está en api/src/.env, ignorada por Git y leída por la herramienta local.

```bash
dotnet build api/src/Telemedicina.sln --configuration Release
python3 api/src/Tools/run_local.py --configuration Release --no-build
```

Portal, desde portalweb/src: npm ci --ignore-scripts y npm run dev.
App, desde app/src: flutter pub get y flutter run.
Swagger de desarrollo: http://localhost:5000/swagger.

La CI usa .NET 10, PostgreSQL temporal, pruebas de contratos/HTTP, SMTP simulado y Playwright.
La batería local reproducible está en api/src/Telemedicina.Tests/Tools/run_milestone_checks.py.
Los resultados actuales se consultan en los informes de cada hito.

## Alcance acordado

Gmail está aplazado: el correo permanece en modo development y las pruebas no envían mensajes reales.
También siguen aplazadas las políticas operativas pendientes, la validación Android/iOS y la revisión PR/UAT.
Video, documentos clínicos y pagos no se habilitan en este hito.
