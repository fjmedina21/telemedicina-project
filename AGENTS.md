# Reglas compartidas — Telemedicina

- Leer la spec `.ai/specs/modulos/Mxx_*.md` y `.ai/specs/ARQUITECTURA_Y_CONTRATOS.md` antes de proponer cambios.
- Seguir el flujo y la puerta de aprobación humana descritos en `.ai/agents/AGENT.md`, junto con el rol detallado en `.ai/agents/*.md`. `.ai/agents/team.json` aún corresponde al pipeline automático legado; no usarlo para omitir la aprobación humana.
- Respetar el área asignada: `frontend` → `app/src/`; `backend` → `api/src/`; `portal` → `portalweb/src/`.
- Backend: un colaborador responsable; Flutter: ambos colaboradores. Revisar por Pull Request.
- No exponer secretos ni datos reales de pacientes. Usar datos sintéticos.
- No ejecutar shell arbitrario, desplegar ni modificar archivos fuera del área autorizada desde el agente.
- Proponer parches, nunca aplicarlos automáticamente. Toda integración requiere revisión y pruebas.
- No inventar decisiones clínicas, seguros, consentimiento o validez legal de recetas.
