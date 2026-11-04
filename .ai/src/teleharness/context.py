"""Selección de contexto desde specs versionadas."""

from __future__ import annotations

from pathlib import Path


class ContextError(ValueError):
    pass


def list_modules(root: Path) -> list[tuple[str, str]]:
    specs = root / "specs" / "modulos"
    found: list[tuple[str, str]] = []
    for path in sorted(specs.glob("M[0-9][0-9]_*.md")):
        title = path.read_text(encoding="utf-8").splitlines()[0].lstrip("# ")
        found.append((path.stem[:3], title))
    return found


def load_context(root: Path, module: str, max_chars: int) -> str:
    paths = sorted((root / "specs" / "modulos").glob(f"{module}_*.md"))
    if len(paths) != 1:
        raise ContextError(f"Módulo desconocido o ambiguo: {module}")
    sections: list[str] = []
    candidates = [
        (paths[0], None),
        (root.parent / "AGENTS.md", 3500),
        (root / "specs" / "ARQUITECTURA_Y_CONTRATOS.md", 7000),
        (root / "specs" / "PLAN_TRABAJO.md", 2700),
        (root / "specs" / "BACKLOG_Y_TRAZABILIDAD.md", 2300),
    ]
    budget = max_chars
    for path, section_cap in candidates:
        content = path.read_text(encoding="utf-8")
        if section_cap is not None:
            content = content[:section_cap]
        label = path.relative_to(root) if path.is_relative_to(root) else path.relative_to(root.parent)
        header = f"### Archivo: {label}\n"
        available = max(0, budget - len(header))
        if path == paths[0] and len(content) > available:
            raise ContextError("El contexto máximo no permite incluir la spec completa del módulo")
        content = content[:available]
        if content:
            sections.append(header + content)
            budget -= len(header) + len(content)
    return "\n\n".join(sections)
