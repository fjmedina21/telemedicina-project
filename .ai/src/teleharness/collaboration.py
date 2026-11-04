"""Convenciones de colaboración locales; GitHub sigue siendo la autoridad real."""

from __future__ import annotations

import json
from pathlib import Path


class CollaborationError(ValueError):
    pass


class CollaborationPolicy:
    def __init__(self, config: dict):
        if config.get("version") != 1:
            raise CollaborationError("collaboration.json tiene versión incompatible")
        self.members = config.get("collaborators", {})
        self.areas = config.get("areas", {})

    @classmethod
    def load(cls, root: Path) -> "CollaborationPolicy":
        try:
            data = json.loads((root / "collaboration.json").read_text(encoding="utf-8"))
        except (OSError, ValueError) as exc:
            raise CollaborationError("No se pudo leer collaboration.json") from exc
        return cls(data)

    def paths_for(self, actor: str, area: str) -> tuple[str, ...]:
        member = self.members.get(actor)
        config = self.areas.get(area)
        if not member or not config:
            raise CollaborationError("Colaborador o área no definida")
        if area not in member.get("allowed_areas", []):
            raise CollaborationError(f"{actor} no puede proponer cambios en área {area}")
        paths = tuple(config.get("write_paths", []))
        if not paths or any(not p.endswith("/") or p.startswith("/") or ".." in p.split("/") for p in paths):
            raise CollaborationError("Rutas de escritura mal configuradas")
        return paths

    @staticmethod
    def validate_files(files: list[str], allowed_paths: tuple[str, ...]) -> None:
        denied = [name for name in files if not any(name.startswith(prefix) and name != prefix[:-1] for prefix in allowed_paths)]
        if denied:
            raise CollaborationError("Parche fuera del área permitida: " + ", ".join(denied[:5]))
