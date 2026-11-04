"""Adaptadores HTTP finos. Ningún SDK de IA es obligatorio."""

from __future__ import annotations

import json
import os
from dataclasses import dataclass
from typing import Protocol
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener


class ProviderError(RuntimeError):
    pass


class AIProvider(Protocol):
    def generate(self, *, system: str, messages: list[dict[str, str]]) -> str: ...


class _NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        # No reenviar tokens a un host distinto tras redirección.
        return None


def _post_json(url: str, headers: dict[str, str], body: dict, timeout: int) -> dict:
    payload = json.dumps(body).encode("utf-8")
    request = Request(url, data=payload, method="POST")
    for name, value in headers.items():
        request.add_header(name, value)
    request.add_header("Content-Type", "application/json")
    try:
        with build_opener(_NoRedirect).open(request, timeout=timeout) as response:
            data = response.read(8 * 1024 * 1024 + 1)
    except HTTPError as exc:
        # No imprimir cuerpo: podría contener información sensible.
        raise ProviderError(f"HTTP {exc.code} desde proveedor") from exc
    except (URLError, TimeoutError) as exc:
        raise ProviderError("No fue posible conectarse al proveedor") from exc
    if len(data) > 8 * 1024 * 1024:
        raise ProviderError("Respuesta demasiado grande")
    try:
        return json.loads(data)
    except (ValueError, UnicodeDecodeError) as exc:
        raise ProviderError("El proveedor devolvió JSON inválido") from exc


def _require_key(name: str) -> str:
    key = os.environ.get(name, "").strip()
    if not key:
        raise ProviderError(f"Falta variable de entorno {name}; no se incluyeron credenciales")
    return key


def validate_compatible_url(base_url: str) -> str:
    parts = urlsplit(base_url)
    if not parts.hostname or parts.username or parts.password or parts.query or parts.fragment:
        raise ProviderError("URL de proveedor compatible inválida")
    if parts.scheme == "https":
        pass
    elif parts.scheme == "http" and parts.hostname.lower() in {"localhost", "127.0.0.1", "::1"}:
        pass
    else:
        raise ProviderError("Endpoint remoto requiere HTTPS; HTTP solo permitido en loopback")
    if parts.path.endswith("/chat/completions"):
        raise ProviderError("Indica URL base (ej. /v1), no el endpoint final")
    return base_url.rstrip("/")


@dataclass
class OpenAIResponsesProvider:
    model: str
    timeout: int = 90
    max_tokens: int = 5000
    key_env: str = "OPENAI_API_KEY"

    def generate(self, *, system: str, messages: list[dict[str, str]]) -> str:
        data = _post_json(
            "https://api.openai.com/v1/responses",
            {"Authorization": f"Bearer {_require_key(self.key_env)}"},
            {
                "model": self.model,
                "instructions": system,
                "input": messages,
                "max_output_tokens": self.max_tokens,
            },
            self.timeout,
        )
        parts: list[str] = []
        for item in data.get("output", []):
            if item.get("type") != "message":
                continue
            for content in item.get("content", []):
                if content.get("type") == "output_text":
                    parts.append(content.get("text", ""))
        if not parts and isinstance(data.get("output_text"), str):
            parts.append(data["output_text"])
        if not parts:
            raise ProviderError("OpenAI no devolvió texto; revisar modelo y presupuesto de salida")
        return "\n".join(parts)


@dataclass
class AnthropicMessagesProvider:
    model: str
    timeout: int = 90
    max_tokens: int = 5000
    key_env: str = "ANTHROPIC_API_KEY"

    def generate(self, *, system: str, messages: list[dict[str, str]]) -> str:
        data = _post_json(
            "https://api.anthropic.com/v1/messages",
            {
                "x-api-key": _require_key(self.key_env),
                "anthropic-version": "2023-06-01",
            },
            {
                "model": self.model,
                "system": system,
                "max_tokens": self.max_tokens,
                "messages": messages,
            },
            self.timeout,
        )
        parts = [item.get("text", "") for item in data.get("content", []) if item.get("type") == "text"]
        if not parts:
            raise ProviderError("Anthropic no devolvió contenido de texto")
        return "\n".join(parts)


@dataclass
class CompatibleChatProvider:
    model: str
    base_url: str = "http://127.0.0.1:11434/v1"
    timeout: int = 90
    max_tokens: int = 5000
    key_env: str = "COMPAT_API_KEY"

    def generate(self, *, system: str, messages: list[dict[str, str]]) -> str:
        url = validate_compatible_url(self.base_url) + "/chat/completions"
        key = os.environ.get(self.key_env, "").strip()
        headers = {"Authorization": f"Bearer {key}"} if key else {}
        data = _post_json(
            url,
            headers,
            {
                "model": self.model,
                "messages": [{"role": "system", "content": system}, *messages],
                "max_tokens": self.max_tokens,
            },
            self.timeout,
        )
        try:
            content = data["choices"][0]["message"]["content"]
            if isinstance(content, str) and content.strip():
                return content
        except (KeyError, IndexError, TypeError):
            pass
        raise ProviderError("Endpoint compatible no devolvió choices[0].message.content")


class MockProvider:
    """Proveedor determinista; cero red, cero credenciales."""

    def generate(self, *, system: str, messages: list[dict[str, str]]) -> str:
        prior_actions = sum(1 for m in messages if m["role"] == "assistant")
        if prior_actions == 0:
            return json.dumps({"action": "list_files", "args": {"path": "."}, "reason": "Inspeccionar workspace"})
        return json.dumps({
            "action": "finish",
            "args": {
                "summary": "Simulación completada. Se listaron archivos sin cambiar código ni contactar un proveedor.",
                "next_steps": ["Seleccionar un proveedor real para generar propuestas de parche", "Revisar la spec del módulo"],
            },
        })


def build_provider(name: str, model: str | None, cfg: dict, base_url: str | None = None) -> AIProvider:
    if name == "mock":
        return MockProvider()
    if not model:
        raise ProviderError("Indica --model para proveedor real")
    options = cfg.get("providers", {}).get(name, {})
    common = {"model": model, "timeout": cfg["request_timeout_seconds"], "max_tokens": cfg["max_output_tokens"]}
    if name == "openai":
        return OpenAIResponsesProvider(**common, key_env=options.get("api_key_env", "OPENAI_API_KEY"))
    if name == "anthropic":
        return AnthropicMessagesProvider(**common, key_env=options.get("api_key_env", "ANTHROPIC_API_KEY"))
    if name == "compatible":
        return CompatibleChatProvider(**common, base_url=base_url or "http://127.0.0.1:11434/v1", key_env=options.get("api_key_env", "COMPAT_API_KEY"))
    raise ProviderError(f"Proveedor no soportado: {name}")
