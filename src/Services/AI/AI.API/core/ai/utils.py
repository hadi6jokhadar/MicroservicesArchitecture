import json
from typing import Any, List, Optional

from fastapi import HTTPException, status
from models import AiProviderSettings, ModelTypeEnum

# ---------------------------------------------------------------------------
# Provider registry
# ---------------------------------------------------------------------------

PROVIDER_ALIASES: dict[str, str] = {
    "openai": "openai",
    "qwen": "openai",
    "qwenai": "openai",
    "azure": "azure",
    "azureopenai": "azure",
    "anthropic": "anthropic",
    "google": "gemini",
    "gemini": "gemini",
    "ollama": "ollama",
    "groq": "groq",
    "mistral": "mistral",
}

# Providers that do not support the response_format parameter.
# Passing it to these providers causes a provider-side 400 error.
PROVIDERS_WITHOUT_RESPONSE_FORMAT: frozenset[str] = frozenset({"anthropic", "ollama"})

# Providers that require an explicit max_tokens value.
PROVIDERS_REQUIRING_MAX_TOKENS: frozenset[str] = frozenset({"anthropic"})

# Default max_tokens injected when the caller omits it for providers that require it.
ANTHROPIC_DEFAULT_MAX_TOKENS: int = 4096


def build_litellm_model(provider: Any, model_name: Any) -> str:
    """Combine provider + model into a LiteLLM model identifier."""
    provider_text = str(provider or "").strip().lower()
    model_text = str(model_name or "").strip()

    normalized_provider = PROVIDER_ALIASES.get(provider_text, provider_text)

    if not normalized_provider:
        return model_text

    # Avoid double-prefixing when caller already qualified the model.
    if "/" in model_text:
        return model_text

    return f"{normalized_provider}/{model_text}"


def normalize_model_type(ai_settings: AiProviderSettings) -> str:
    """Return lower-case model type string from AiProviderSettings."""
    raw = getattr(ai_settings, "ModelType", ModelTypeEnum.Text)
    if isinstance(raw, ModelTypeEnum):
        return raw.value.lower()
    return str(raw or ModelTypeEnum.Text.value).strip().lower()


_VALID_RESPONSE_FORMAT_TYPES = {"text", "json_object", "json_schema"}


def parse_response_format(raw: Optional[str]) -> Optional[dict]:
    """Parse a JSON string into a response_format dict; returns None on any failure.

    The dict must conform to the OpenAI/LiteLLM spec:
      - {"type": "text"}
      - {"type": "json_object"}
      - {"type": "json_schema", "json_schema": {...}}
    Any dict that lacks a valid string "type" key is rejected and None is returned.
    """
    if not raw:
        return None
    try:
        parsed = json.loads(raw)
    except (json.JSONDecodeError, ValueError):
        return None

    if not isinstance(parsed, dict):
        return None

    format_type = parsed.get("type")
    if not isinstance(format_type, str) or format_type not in _VALID_RESPONSE_FORMAT_TYPES:
        return None

    return parsed


def extract_user_id(auth: dict) -> Optional[int]:
    """Extract integer user id from JWT payload when available."""
    user_id_str: Optional[str] = auth.get("payload", {}).get("nameid")
    return int(user_id_str) if user_id_str and user_id_str.isdigit() else None


def estimate_tokens_if_missing(
    prompt_tokens: int,
    completion_tokens: int,
    litellm_messages: List[dict],
    complete_response: str,
) -> tuple[int, int]:
    """Fall back to character-count estimates when the provider returns zero token counts."""
    estimated_prompt = (
        prompt_tokens if prompt_tokens != 0 else max(1, len(str(litellm_messages)) // 4)
    )
    estimated_completion = (
        completion_tokens if completion_tokens != 0 else max(1, len(complete_response) // 4)
    )
    return estimated_prompt, estimated_completion


# ---------------------------------------------------------------------------
# Unified provider error mapper
# ---------------------------------------------------------------------------

def map_litellm_exception_to_http(exc: Exception) -> HTTPException:
    """Map a LiteLLM provider exception to a standardized FastAPI HTTPException.

    Catches the full litellm exception hierarchy and translates it into the
    HTTP status codes callers expect:
        RateLimitError            -> 429
        ContextWindowExceededError -> 400
        AuthenticationError       -> 401
        BadRequestError           -> 400
        NotFoundError             -> 404
        ServiceUnavailableError   -> 503
        Timeout                   -> 504
        <any other>               -> 500
    """
    try:
        import litellm.exceptions as _le

        _mapping: list[tuple[type, int]] = [
            (_le.RateLimitError, status.HTTP_429_TOO_MANY_REQUESTS),
            (_le.ContextWindowExceededError, status.HTTP_400_BAD_REQUEST),
            (_le.AuthenticationError, status.HTTP_401_UNAUTHORIZED),
            (_le.BadRequestError, status.HTTP_400_BAD_REQUEST),
            (_le.NotFoundError, status.HTTP_404_NOT_FOUND),
            (_le.ServiceUnavailableError, status.HTTP_503_SERVICE_UNAVAILABLE),
            (_le.Timeout, status.HTTP_504_GATEWAY_TIMEOUT),
        ]
        for exc_type, status_code in _mapping:
            if isinstance(exc, exc_type):
                return HTTPException(status_code=status_code, detail=str(exc))
    except (ImportError, AttributeError):
        pass

    return HTTPException(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        detail=str(exc) or "Unexpected AI provider error.",
    )
