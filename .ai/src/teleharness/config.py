"""Configuración sin credenciales embebidas."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT_ROOT = ROOT.parent


class ConfigurationError(ValueError):
    pass


def load_config(root: Path = ROOT) -> dict:
    path = root / "harness.json"
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ConfigurationError(f"No se pudo cargar harness.json: {exc}") from exc
    if data.get("version") != 1:
        raise ConfigurationError("Versión de configuración no compatible")
    return data
