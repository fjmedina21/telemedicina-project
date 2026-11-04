"""Interfaz de línea de comandos del harness de telemedicina."""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path
from uuid import uuid4

from .agent import ROLES, AgentEngine, AgentError
from .collaboration import CollaborationPolicy, CollaborationError
from .config import ROOT, PROJECT_ROOT, ConfigurationError, load_config
from .context import ContextError, list_modules, load_context
from .providers import ProviderError, build_provider
from .tools import ToolError, WorkspaceTools, apply_git_patch, validate_patch


RUN_ID_FORMAT = re.compile(r"[0-9a-f]{12}")
MODULE_FORMAT = re.compile(r"M[0-9][0-9]")
CHECK_PRESETS = {
    "flutter-analyze": ["flutter", "analyze"],
    "flutter-test": ["flutter", "test"],
    "dotnet-test": ["dotnet", "test"],
    "python-tests": ["python3", "-m", "unittest", "discover", "-s", ".ai/tests", "-v"],
}


def make_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="teleharness", description="Harness agnóstico para proyecto Telemedicina")
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("doctor", help="Comprueba herramientas y configuración sin exponer claves")
    sub.add_parser("list-modules", help="Lista módulos de la spec")

    for name in ("run", "team"):
        q = sub.add_parser(name, help="Ejecuta un rol" if name == "run" else "Ejecuta equipo completo por etapas")
        q.add_argument("--workspace", type=Path, default=PROJECT_ROOT)
        q.add_argument("--module", required=True, help="M01..M12")
        q.add_argument("--task", required=True)
        q.add_argument("--actor", choices=["backend_mobile", "mobile"], required=True)
        q.add_argument("--area", choices=["frontend", "backend", "portal", "fullstack"], required=True)
        q.add_argument("--provider", choices=["mock", "openai", "anthropic", "compatible"], default=None)
        q.add_argument("--model", default=None)
        q.add_argument("--base-url", default=None, help="Solo proveedor compatible")
        if name == "run":
            q.add_argument("--role", choices=ROLES, default="implementer")
            q.add_argument("--max-steps", type=int, default=None)

    q = sub.add_parser("review", help="Muestra el parche pendiente")
    q.add_argument("--run-id", required=True)

    q = sub.add_parser("apply", help="Aplica parche previamente revisado")
    q.add_argument("--run-id", required=True)
    q.add_argument("--workspace", required=True, type=Path)
    q.add_argument("--actor", choices=["backend_mobile", "mobile"], required=True)
    q.add_argument("--area", choices=["frontend", "backend", "portal", "fullstack"], required=True)
    q.add_argument("--yes", action="store_true", help="Confirma aplicación sin pregunta interactiva")

    q = sub.add_parser("check", help="Ejecuta verificación predefinida, sin shell libre")
    q.add_argument("--preset", choices=sorted(CHECK_PRESETS), required=True)
    q.add_argument("--workspace", type=Path, required=True)
    q.add_argument("--timeout", type=int, default=300)
    return p


def get_run_dir(run_id: str) -> Path:
    if not RUN_ID_FORMAT.fullmatch(run_id):
        raise ValueError("ID de ejecución inválido")
    path = ROOT / "runs" / run_id
    if not path.is_dir() or path.is_symlink():
        raise ValueError("Ejecución no encontrada")
    return path


def make_runtime(args, cfg: dict):
    if not MODULE_FORMAT.fullmatch(args.module):
        raise ValueError("El módulo debe tener formato M01..M12")
    workspace = args.workspace.expanduser().resolve()
    if not workspace.is_dir():
        raise ValueError(f"El workspace no existe: {workspace}")
    allowed_paths = CollaborationPolicy.load(ROOT).paths_for(args.actor, args.area)
    tools = WorkspaceTools(
        workspace, cfg["max_read_bytes"], cfg["max_tool_result_chars"], cfg["max_patch_bytes"],
        allowed_paths=allowed_paths,
    )
    provider_name = args.provider or cfg["default_provider"]
    provider = build_provider(provider_name, args.model, cfg, args.base_url)
    context = load_context(ROOT, args.module, cfg["max_context_chars"])
    return workspace, tools, provider, provider_name, context


def execute_single(args, cfg: dict) -> int:
    _, tools, provider, provider_name, context = make_runtime(args, cfg)
    steps = args.max_steps or cfg["default_max_steps"]
    if steps < 1 or steps > cfg["max_steps_limit"]:
        raise ValueError("Cantidad de pasos fuera de límites")
    run_id = uuid4().hex[:12]
    run_dir = ROOT / "runs" / run_id
    run_dir.parent.mkdir(exist_ok=True)
    engine = AgentEngine(provider, tools, ROOT, max_steps=steps)
    result = engine.run(module=args.module, role=args.role, task=args.task, context=context, run_dir=run_dir,
                        actor=args.actor, area=args.area)
    result["provider"] = provider_name
    (run_dir / "result.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Run: {run_id} | Rol: {args.role} | {args.actor}/{args.area} | Estado: {result['status']}")
    print(result["summary"])
    if result["patch_files"]:
        print(f"Parche pendiente para {', '.join(result['patch_files'])}. Revisa: review --run-id {run_id}")
    return 0 if result["status"] == "completed" else 2


def execute_team(args, cfg: dict) -> int:
    workspace, tools, provider, provider_name, context = make_runtime(args, cfg)
    team_config = json.loads((ROOT / "agents" / "team.json").read_text(encoding="utf-8"))
    members = team_config.get("roles", [])
    if [r.get("name") for r in members] != list(ROLES):
        raise ValueError("team.json debe conservar orden y roles autorizados")
    run_id = uuid4().hex[:12]
    run_dir = ROOT / "runs" / run_id
    run_dir.mkdir(parents=True, exist_ok=False)
    handoff_sections: list[str] = []
    member_results: list[dict] = []
    patch_files: list[str] = []
    for index, spec in enumerate(members, 1):
        role = spec["name"]
        steps = int(spec["max_steps"])
        if steps < 1 or steps > cfg["max_steps_limit"]:
            raise ValueError("Límite de pasos de team.json inválido")
        print(f"[{index}/{len(members)}] {role} ...", flush=True)
        engine = AgentEngine(provider, tools, ROOT, max_steps=steps)
        role_dir = run_dir / f"{index:02d}_{role}"
        handoff = "\n\n".join(handoff_sections)[-20000:]
        if role == "validator" and (run_dir / "proposal.patch").exists():
            handoff += "\n\n# PARCHE PROPUESTO (no aplicado)\n" + (run_dir / "proposal.patch").read_text(encoding="utf-8")[:12000]
        result = engine.run(module=args.module, role=role, task=args.task, context=context, run_dir=role_dir,
                            handoff=handoff, actor=args.actor, area=args.area)
        member_results.append({"role": role, "status": result["status"], "summary": result["summary"]})
        if result["status"] != "completed":
            break
        artifact_file = role_dir / "deliverable.md"
        artifact = artifact_file.read_text(encoding="utf-8") if artifact_file.exists() else result["summary"]
        handoff_sections.append(f"## {role}\n{artifact[:7000]}")
        if role == "implementer" and (role_dir / "proposal.patch").is_file():
            shutil.copyfile(role_dir / "proposal.patch", run_dir / "proposal.patch")
            patch_files = result["patch_files"]
    completed = len(member_results) == len(members) and all(r["status"] == "completed" for r in member_results)
    meta = {
        "type": "team", "module": args.module, "provider": provider_name, "workspace": str(workspace),
        "actor": args.actor, "area": args.area,
        "status": "completed" if completed else "incomplete", "members": member_results, "patch_files": patch_files,
    }
    (run_dir / "result.json").write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Team run: {run_id} | Estado: {meta['status']}")
    print(f"Entregables: {run_dir}")
    if patch_files:
        print(f"Parche sin aplicar: {', '.join(patch_files)}. Revisa: review --run-id {run_id}")
    return 0 if completed else 2


def run_review(run_id: str) -> int:
    run_dir = get_run_dir(run_id)
    patch = run_dir / "proposal.patch"
    if not patch.is_file():
        print("Esta ejecución no tiene un parche propuesto")
        return 1
    print(patch.read_text(encoding="utf-8"))
    return 0


def run_apply(run_id: str, workspace: Path, yes: bool, actor: str, area: str) -> int:
    run_dir = get_run_dir(run_id)
    patch_file = run_dir / "proposal.patch"
    if not patch_file.is_file() or patch_file.is_symlink():
        raise ValueError("No existe parche pendiente")
    data = json.loads((run_dir / "result.json").read_text(encoding="utf-8"))
    target = workspace.expanduser().resolve()
    if str(target) != data.get("workspace"):
        raise ValueError("El workspace no coincide con el de la ejecución original")
    if actor != data.get("actor") or area != data.get("area"):
        raise ValueError("El actor/área no coincide con el que generó el parche")
    allowed_paths = CollaborationPolicy.load(ROOT).paths_for(actor, area)
    if (run_dir / "applied.json").exists():
        raise ValueError("Este parche ya fue marcado como aplicado")
    patch = patch_file.read_text(encoding="utf-8")
    files = validate_patch(patch, target)
    CollaborationPolicy.validate_files(files, allowed_paths)
    print(f"Parche {run_id} afecta: {', '.join(files)}")
    print("Revisa `review --run-id` antes de confirmar. Esta acción modifica archivos.")
    if not yes:
        answer = input("Escribe APLICAR para continuar: ").strip()
        if answer != "APLICAR":
            print("Cancelado")
            return 1
    apply_git_patch(target, patch, allowed_paths)
    (run_dir / "applied.json").write_text(json.dumps({"workspace": str(target), "files": files}, indent=2) + "\n", encoding="utf-8")
    print("Parche aplicado. Ejecuta las pruebas y revisa los cambios con git diff.")
    return 0


def run_check(workspace: Path, preset: str, timeout: int) -> int:
    path = workspace.expanduser().resolve()
    if not path.is_dir():
        raise ValueError("Workspace no existe")
    if not 1 <= timeout <= 1800:
        raise ValueError("Timeout permitido: 1..1800 segundos")
    command = CHECK_PRESETS[preset]
    if preset.startswith("flutter-"):
        path = path / "app" / "src"
        if not (path / "pubspec.yaml").is_file():
            raise ValueError("Aún no existe proyecto Flutter en app/src")
    elif preset == "dotnet-test":
        path = path / "api" / "src"
        if not any(path.rglob("*.csproj")) and not any(path.rglob("*.sln")):
            raise ValueError("Aún no existe proyecto .NET en api/src")
    print(f"Ejecutando preset: {preset} (comando fijo)", flush=True)
    try:
        result = subprocess.run(command, cwd=path, timeout=timeout, capture_output=True, text=True, check=False)
    except FileNotFoundError as exc:
        raise ValueError(f"No se encuentra el ejecutable requerido: {command[0]}") from exc
    except subprocess.TimeoutExpired as exc:
        raise ValueError("Verificación excedió el límite de tiempo") from exc
    print((result.stdout + "\n" + result.stderr)[-12000:])
    print(f"Código de salida: {result.returncode}")
    return result.returncode


def main(argv: list[str] | None = None) -> int:
    args = make_parser().parse_args(argv)
    try:
        cfg = load_config()
        if args.command == "doctor":
            print(f"Harness v0.4 | Python {sys.version_info.major}.{sys.version_info.minor}")
            for name, binary in [("Git", "git"), ("Flutter", "flutter"), (".NET", "dotnet")]:
                print(f"{name}: {'disponible' if shutil.which(binary) else 'no instalado'}")
            for name in ("OPENAI_API_KEY", "ANTHROPIC_API_KEY", "COMPAT_API_KEY"):
                print(f"{name}: {'presente' if os.environ.get(name) else 'no configurada'}")
            print("Proveedor de pruebas: mock (sin credenciales)")
            print("Colaboración: backend_mobile (backend + Flutter), mobile (Flutter)")
            return 0
        if args.command == "list-modules":
            for module, title in list_modules(ROOT):
                print(f"{module}: {title}")
            return 0
        if args.command == "run":
            return execute_single(args, cfg)
        if args.command == "team":
            return execute_team(args, cfg)
        if args.command == "review":
            return run_review(args.run_id)
        if args.command == "apply":
            return run_apply(args.run_id, args.workspace, args.yes, args.actor, args.area)
        if args.command == "check":
            return run_check(args.workspace, args.preset, args.timeout)
    except (ValueError, AgentError, ToolError, ProviderError, ConfigurationError, ContextError, CollaborationError, OSError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 2
    return 2
