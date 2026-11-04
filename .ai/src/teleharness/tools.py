"""Herramientas de lectura y parche propuesto, confinadas al workspace."""

from __future__ import annotations

import os
import subprocess
from pathlib import Path, PurePosixPath

from .collaboration import CollaborationPolicy, CollaborationError


class ToolError(ValueError):
    pass


DENIED_PARTS = {
    ".git", ".harness", "runs", ".venv", "venv", "node_modules", ".dart_tool",
    "build", "dist", "Pods", "__pycache__", ".idea", ".gradle", "coverage",
}
DENIED_NAMES = {"id_rsa", "id_ed25519", "credentials.json", "secrets.json", "google-services.json", "GoogleService-Info.plist"}
DENIED_EXTENSIONS = {".pem", ".p12", ".pfx", ".keystore", ".jks", ".key"}


def validate_relative_path(value: str) -> PurePosixPath:
    if not isinstance(value, str) or not value or "\\" in value or "\x00" in value:
        raise ToolError("Ruta inválida")
    path = PurePosixPath(value)
    if path.is_absolute() or ".." in path.parts:
        raise ToolError("Ruta fuera del workspace")
    for part in path.parts:
        if part in DENIED_PARTS or part in DENIED_NAMES or part.startswith(".env") or PurePosixPath(part).suffix.lower() in DENIED_EXTENSIONS:
            raise ToolError("Archivo o directorio protegido")
    return path


def safe_path(workspace: Path, value: str, must_exist: bool = True) -> Path:
    relative = validate_relative_path(value)
    cursor = workspace
    for part in relative.parts:
        cursor = cursor / part
        if cursor.is_symlink():
            raise ToolError("Rutas con symlinks no permitidas")
    target = (workspace / str(relative)).resolve()
    try:
        target.relative_to(workspace.resolve())
    except ValueError as exc:
        raise ToolError("Ruta o symlink fuera del workspace") from exc
    if must_exist and not target.exists():
        raise ToolError("Archivo o carpeta no existe")
    return target


def validate_patch(patch: str, workspace: Path, max_bytes: int = 131072) -> list[str]:
    if not isinstance(patch, str) or len(patch.encode("utf-8")) > max_bytes:
        raise ToolError("Parche vacío, inválido o demasiado grande")
    if not patch.startswith("diff --git a/") or "\x00" in patch:
        raise ToolError("Se requiere diff unificado formato git")
    files: list[str] = []
    old_headers = new_headers = 0
    current_file = ""
    forbidden = ("GIT binary patch", "Binary files ", "deleted file mode", "rename from ", "rename to ", "copy from ", "copy to ", "old mode ", "new mode ", "similarity index ")
    for line in patch.splitlines():
        if line.startswith(forbidden):
            raise ToolError("El harness no permite cambios binarios, borrados, renombres ni permisos")
        if line.startswith("new file mode ") and line != "new file mode 100644":
            raise ToolError("Solo se permite crear archivos regulares no ejecutables")
        if line.startswith("diff --git "):
            tokens = line.split(" ")
            if len(tokens) != 4 or not tokens[2].startswith("a/") or not tokens[3].startswith("b/"):
                raise ToolError("Nombre de archivo no válido en diff")
            a, b = tokens[2][2:], tokens[3][2:]
            if a != b:
                raise ToolError("Renombres no permitidos")
            safe_path(workspace, b, must_exist=False)
            files.append(b)
            current_file = b
        elif line.startswith("--- "):
            old_headers += 1
            raw = line[4:].split("\t", 1)[0]
            if raw != "/dev/null":
                if not raw.startswith("a/"):
                    raise ToolError("Cabecera antigua inválida")
                safe_path(workspace, raw[2:], must_exist=False)
                if raw[2:] != current_file:
                    raise ToolError("La cabecera antigua no coincide con diff")
        elif line.startswith("+++ "):
            new_headers += 1
            raw = line[4:].split("\t", 1)[0]
            if raw == "/dev/null" or not raw.startswith("b/"):
                raise ToolError("Borrado de archivos no permitido")
            safe_path(workspace, raw[2:], must_exist=False)
            if raw[2:] != current_file:
                raise ToolError("La cabecera nueva no coincide con diff")
    if not files or old_headers != len(files) or new_headers != len(files):
        raise ToolError("Cabeceras del diff incompletas")
    if len(set(files)) != len(files):
        raise ToolError("Archivo repetido en diff")
    return files


def check_git_patch(workspace: Path, patch: str) -> None:
    try:
        result = subprocess.run(
            ["git", "apply", "--check", "--whitespace=error", "-"],
            input=patch,
            text=True,
            cwd=workspace,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=25,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise ToolError("No se pudo validar parche con git") from exc
    if result.returncode != 0:
        raise ToolError("El parche no aplica limpiamente; revisar contexto y formato")


def apply_git_patch(workspace: Path, patch: str, allowed_paths: tuple[str, ...] | None = None) -> None:
    files = validate_patch(patch, workspace)
    if allowed_paths is not None:
        CollaborationPolicy.validate_files(files, allowed_paths)
    check_git_patch(workspace, patch)
    result = subprocess.run(
        ["git", "apply", "--whitespace=error", "-"],
        input=patch,
        text=True,
        cwd=workspace,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=25,
        check=False,
    )
    if result.returncode != 0:
        raise ToolError("No se pudo aplicar parche. No reintentes sin inspeccionar el workspace")


class WorkspaceTools:
    def __init__(self, workspace: Path, read_limit: int = 65536, result_limit: int = 12000,
                 patch_limit: int = 131072, allowed_paths: tuple[str, ...] | None = None):
        if not workspace.is_dir():
            raise ToolError("Workspace debe existir y ser un directorio")
        self.workspace = workspace.resolve()
        self.read_limit = read_limit
        self.result_limit = result_limit
        self.patch_limit = patch_limit
        self.allowed_paths = allowed_paths

    def list_files(self, path: str = ".") -> dict:
        base = safe_path(self.workspace, path)
        if not base.is_dir():
            raise ToolError("No es un directorio")
        files = []
        for root, dirs, names in os.walk(base, followlinks=False):
            dirs[:] = sorted(d for d in dirs if not Path(root, d).is_symlink() and self._visible(Path(root, d)))
            for name in sorted(names):
                item = Path(root, name)
                if not item.is_file() or not self._visible(item):
                    continue
                files.append(item.relative_to(self.workspace).as_posix())
                if len(files) >= 100:
                    return {"files": files, "truncated": True}
        return {"files": files, "truncated": False}

    def _visible(self, item: Path) -> bool:
        try:
            safe_path(self.workspace, item.relative_to(self.workspace).as_posix())
            return not item.is_symlink()
        except ToolError:
            return False

    def read_file(self, path: str) -> dict:
        target = safe_path(self.workspace, path)
        if not target.is_file() or target.is_symlink():
            raise ToolError("No es un archivo regular")
        if target.stat().st_size > self.read_limit:
            raise ToolError("Archivo demasiado grande para lectura de agente")
        try:
            content = target.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as exc:
            raise ToolError("Archivo no es texto UTF-8") from exc
        return {"path": path, "content": content[:self.result_limit], "truncated": len(content) > self.result_limit}

    def search_text(self, query: str, path: str = ".") -> dict:
        if not query or len(query) > 200 or "\n" in query:
            raise ToolError("Consulta inválida")
        base = safe_path(self.workspace, path)
        candidates = [base] if base.is_file() else []
        if base.is_dir():
            for root, dirs, names in os.walk(base, followlinks=False):
                dirs[:] = [d for d in dirs if not Path(root, d).is_symlink() and self._visible(Path(root, d))]
                for name in names:
                    p = Path(root, name)
                    if self._visible(p) and p.is_file():
                        candidates.append(p)
                    if len(candidates) >= 200:
                        break
                if len(candidates) >= 200:
                    break
        results = []
        for candidate in candidates:
            if candidate.stat().st_size > self.read_limit:
                continue
            try:
                lines = candidate.read_text(encoding="utf-8").splitlines()
            except (OSError, UnicodeDecodeError):
                continue
            for number, line in enumerate(lines, 1):
                if query.casefold() in line.casefold():
                    results.append({"path": candidate.relative_to(self.workspace).as_posix(), "line": number, "text": line[:250]})
                    if len(results) >= 40:
                        return {"matches": results, "truncated": True}
        return {"matches": results, "truncated": False}

    def verify_patch(self, patch: str) -> dict:
        files = validate_patch(patch, self.workspace, self.patch_limit)
        if self.allowed_paths is not None:
            CollaborationPolicy.validate_files(files, self.allowed_paths)
        check_git_patch(self.workspace, patch)
        return {"valid": True, "files": files, "requires_human_apply": True}
