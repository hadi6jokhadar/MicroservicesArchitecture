import uuid
from typing import Any, List, Optional, TypedDict

from fastapi import HTTPException, status
from langgraph.graph import END, START, StateGraph
from sqlalchemy.ext.asyncio import AsyncSession

from core.ai.db_queries import get_settings_by_key, get_system_prompt_by_key
from core.ai.file_context import file_manager_client
from core.ai.multimodal_utils import (
    PROVIDERS_SUPPORTING_AUDIO,
    PROVIDERS_SUPPORTING_VISION,
    QWEN_RAW_PROVIDERS,
    build_media_content_blocks,
    resolve_audio_format,
)
from core.ai.schemas import ChatRequest
from core.ai.sessions import resolve_or_create_session
from core.ai.utils import (
    ANTHROPIC_DEFAULT_MAX_TOKENS,
    PROVIDER_ALIASES,
    PROVIDERS_REQUIRING_MAX_TOKENS,
    PROVIDERS_WITHOUT_RESPONSE_FORMAT,
    build_litellm_model,
    extract_user_id,
    normalize_model_type,
    parse_response_format,
)
from models import AiProviderSettings, AudioDataModeEnum, ModelTypeEnum


# ---------------------------------------------------------------------------
# LangGraph state
# ---------------------------------------------------------------------------

class ChatWorkflowState(TypedDict):
    request: ChatRequest
    provider: str
    provider_normalized: str  # resolved via PROVIDER_ALIASES (e.g. "anthropic", "openai")
    model_name: str
    api_key: str
    system_prompt: Optional[str]
    response_format: Optional[dict]
    # Content elements may be str or a list of content blocks (multimodal).
    litellm_messages: List[dict[str, Any]]
    litellm_model: str
    max_completion_tokens: Optional[int]
    # Multimodal fields — populated by _multimodal_transform_node.
    file_ids: List[int]
    tenant_id: Optional[str]
    has_vision_attachments: bool
    has_audio_attachments: bool
    # AudioDataMode from AiProviderSettings: None/Auto = auto-detect, Url = force URL, Base64 = force base64.
    audio_data_mode: Optional[str]


# ---------------------------------------------------------------------------
# Runtime context returned to the route handler
# ---------------------------------------------------------------------------

class ChatRuntimeContext(TypedDict):
    user_id: Optional[int]
    session_id: uuid.UUID
    # Content elements may be str or a list of content blocks (multimodal).
    litellm_messages: List[dict[str, Any]]
    litellm_model: str
    provider_api_key: str
    provider_api_base: Optional[str]
    model_name: str
    user_content: str
    system_prompt: Optional[str]
    response_format: Optional[dict]
    max_completion_tokens: Optional[int]
    temperature: Optional[float]
    top_p: Optional[float]
    frequency_penalty: Optional[float]
    presence_penalty: Optional[float]


# ---------------------------------------------------------------------------
# Workflow nodes
# ---------------------------------------------------------------------------

def _normalize_provider_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Resolve the raw provider string to a canonical LiteLLM prefix via PROVIDER_ALIASES.

    This is the entry-point node and the single source of truth for which
    provider-specific branches are taken further down the graph.
    """
    raw = str(state["provider"] or "").strip().lower()
    normalized = PROVIDER_ALIASES.get(raw, raw)
    return {**state, "provider_normalized": normalized}


def _prepare_messages_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Build the LiteLLM message list, prepending the system prompt when present.

    Content is initially a plain string per message.  The multimodal_transform
    node that follows may upgrade the last user message's content to a list of
    typed content blocks when file attachments are present.
    """
    messages: List[dict[str, Any]] = []
    if state["system_prompt"]:
        messages.append({"role": "system", "content": state["system_prompt"]})
    messages.extend(
        {"role": msg.role, "content": msg.content}
        for msg in (state["request"].messages or [])
    )
    return {**state, "litellm_messages": messages}


def _anthropic_transform_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Apply Anthropic-specific transformations before the pre-flight check.

    1. Enforce max_tokens — Anthropic requires an explicit value and will return
       a 400 if it is omitted.  Fall back to ANTHROPIC_DEFAULT_MAX_TOKENS.
    2. System messages — LiteLLM converts the leading system-role message to
       Anthropic's top-level `system` field automatically; no structural change
       is needed here, but the enforcement ensures nothing slips through.
    """
    max_tokens = state["max_completion_tokens"]
    if max_tokens is None:
        max_tokens = ANTHROPIC_DEFAULT_MAX_TOKENS
    return {**state, "max_completion_tokens": max_tokens}


def _preflight_validation_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Strip unsupported parameters and enforce provider media capability.

    1. Strips response_format for providers that do not accept it.
    2. Raises HTTP 400 when audio attachments are sent to a provider that does
       not support audio input content blocks.
    3. Raises HTTP 400 when image attachments are sent to a provider that does
       not support vision input content blocks.
    """
    response_format = state["response_format"]
    provider = state["provider_normalized"]

    if provider in PROVIDERS_WITHOUT_RESPONSE_FORMAT:
        response_format = None

    if state.get("has_audio_attachments") and provider not in PROVIDERS_SUPPORTING_AUDIO:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=(
                f"Provider '{provider}' does not support audio input. "
                "Select a provider that supports audio (e.g. openai, gemini)."
            ),
        )

    if state.get("has_vision_attachments") and provider not in PROVIDERS_SUPPORTING_VISION:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=(
                f"Provider '{provider}' does not support image input. "
                "Select a vision-capable provider (e.g. openai, anthropic, gemini)."
            ),
        )

    return {**state, "response_format": response_format}


def _resolve_model_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Resolve the fully-qualified LiteLLM model identifier."""
    return {
        **state,
        "litellm_model": build_litellm_model(state["provider"], state["model_name"]),
    }


async def _multimodal_transform_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Fetch attached files and inject provider-specific content blocks into the
    last user message.

    When file_ids is empty this node is a no-op and returns state unchanged.
    For each attached file the node:
      - Resolves metadata via the FileManager client.
      - Downloads raw bytes and determines the MIME type.
      - Encodes image files as image_url blocks (Base64 data URL).
      - Encodes audio files using the format appropriate for the provider:
          • OpenAI / Azure / Groq / Gemini → input_audio block.
          • Qwen omni (Dashscope)           → audio_url block.
          • Anthropic / Claude              → text description fallback
                                               (no native audio API).
      - Represents document files as text context blocks (URL + filename).
    The last user message’s content string is replaced with a typed content
    list: [{"type": "text", "text": ...}, <media blocks…>].
    LiteLLM’s adapter layer translates these blocks into each provider’s
    proprietary wire format.
    """
    if not state["file_ids"]:
        return state

    files_metadata = await file_manager_client.get_files_by_ids(
        state["file_ids"], state["tenant_id"]
    )
    if not files_metadata:
        return state

    raw_provider = str(state["provider"] or "").strip().lower()
    audio_fmt = resolve_audio_format(raw_provider, state["provider_normalized"])

    # Setting-level override: AudioDataMode.Url → "audio_url",
    # AudioDataMode.Base64 → "input_audio" (except text_fallback providers like Claude).
    setting_mode = (state.get("audio_data_mode") or "").strip().lower()
    if setting_mode == "url" and audio_fmt != "text_fallback":
        audio_fmt = "audio_url"
    elif setting_mode == "base64" and audio_fmt != "text_fallback":
        audio_fmt = "input_audio"

    media_blocks, has_image, has_audio = await build_media_content_blocks(
        files_metadata, audio_format=audio_fmt
    )
    if not media_blocks:
        return state

    messages: List[dict[str, Any]] = list(state["litellm_messages"])

    last_user_idx: Optional[int] = None
    for i in range(len(messages) - 1, -1, -1):
        if messages[i].get("role") == "user":
            last_user_idx = i
            break

    if last_user_idx is None:
        messages.append({"role": "user", "content": media_blocks})
        return {
            **state,
            "litellm_messages": messages,
            "has_vision_attachments": has_image,
            "has_audio_attachments": has_audio,
        }

    last_msg = messages[last_user_idx]
    existing_text = last_msg.get("content", "")

    content_list: List[dict[str, Any]] = []
    if existing_text:
        text_str = existing_text if isinstance(existing_text, str) else str(existing_text)
        content_list.append({"type": "text", "text": text_str})
    content_list.extend(media_blocks)

    messages[last_user_idx] = {**last_msg, "content": content_list}

    return {
        **state,
        "litellm_messages": messages,
        "has_vision_attachments": has_image,
        "has_audio_attachments": has_audio,
    }


# ---------------------------------------------------------------------------
# Conditional router
# ---------------------------------------------------------------------------

def _route_by_provider(state: ChatWorkflowState) -> str:
    """Return the branch key for the conditional edge after multimodal_transform."""
    return "anthropic" if state["provider_normalized"] == "anthropic" else "default"


# ---------------------------------------------------------------------------
# Compiled graph (module-level singleton)
# ---------------------------------------------------------------------------

def _build_chat_workflow():
    workflow = StateGraph(ChatWorkflowState)

    # Register nodes
    workflow.add_node("normalize_provider", _normalize_provider_node)
    workflow.add_node("prepare_messages", _prepare_messages_node)
    workflow.add_node("multimodal_transform", _multimodal_transform_node)
    workflow.add_node("anthropic_transform", _anthropic_transform_node)
    workflow.add_node("preflight_validation", _preflight_validation_node)
    workflow.add_node("resolve_model", _resolve_model_node)

    # Linear entry: normalize → build messages → inject multimodal blocks
    workflow.add_edge(START, "normalize_provider")
    workflow.add_edge("normalize_provider", "prepare_messages")
    workflow.add_edge("prepare_messages", "multimodal_transform")

    # Branch after multimodal_transform: Anthropic gets an extra transformation step
    workflow.add_conditional_edges(
        "multimodal_transform",
        _route_by_provider,
        {"anthropic": "anthropic_transform", "default": "preflight_validation"},
    )

    # Anthropic rejoins the main path after its transform
    workflow.add_edge("anthropic_transform", "preflight_validation")

    # Final linear path: capability guard + strip unsupported params → build model string
    workflow.add_edge("preflight_validation", "resolve_model")
    workflow.add_edge("resolve_model", END)

    return workflow.compile()


CHAT_WORKFLOW = _build_chat_workflow()


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

async def run_chat_orchestration(
    request: ChatRequest,
    ai_settings: AiProviderSettings,
    system_prompt: Optional[str] = None,
    response_format: Optional[dict] = None,
    max_completion_tokens: Optional[int] = None,
    tenant_id: Optional[str] = None,
) -> ChatWorkflowState:
    """Run the LangGraph chat workflow and return the final state."""
    audio_data_mode_value: Optional[str] = (
        ai_settings.AudioDataMode.value
        if ai_settings.AudioDataMode is not None
        else None
    )
    initial_state: ChatWorkflowState = {
        "request": request,
        "provider": str(ai_settings.Provider),
        "provider_normalized": "",  # populated by normalize_provider node
        "model_name": str(ai_settings.ModelName),
        "api_key": str(ai_settings.ApiKey),
        "system_prompt": system_prompt,
        "response_format": response_format,
        "litellm_messages": [],
        "litellm_model": "",
        "max_completion_tokens": max_completion_tokens,
        "file_ids": list(request.file_ids),
        "tenant_id": tenant_id,
        "has_vision_attachments": False,
        "has_audio_attachments": False,
        "audio_data_mode": audio_data_mode_value,
    }
    return await CHAT_WORKFLOW.ainvoke(initial_state)  # type: ignore


async def build_chat_runtime_context(
    request: ChatRequest,
    tenant_id: Optional[str],
    db: AsyncSession,
    auth: dict,
) -> ChatRuntimeContext:
    """Resolve all dependencies for a chat request and return a ready-to-use runtime context."""
    user_id = extract_user_id(auth)
    ai_settings = await get_settings_by_key(request.settings_key, tenant_id, db)

    # Caller-supplied max_completion_tokens overrides the DB setting.
    max_completion_tokens: Optional[int] = (
        request.max_completion_tokens
        if request.max_completion_tokens is not None
        else ai_settings.MaxCompletionTokens
    )

    # Resolve system prompt and optional response_format.
    system_prompt: Optional[str] = None
    response_format: Optional[dict] = None
    if request.system_prompt_key:
        prompt_obj = await get_system_prompt_by_key(request.system_prompt_key, tenant_id, db)
        if prompt_obj is not None:
            system_prompt = prompt_obj.PromptText
            response_format = parse_response_format(
                getattr(prompt_obj, "ResponseFormat", None)
            )

    session_id = await resolve_or_create_session(request.session_id, tenant_id, user_id, db)

    orchestration_state = await run_chat_orchestration(
        request, ai_settings, system_prompt, response_format, max_completion_tokens, tenant_id
    )

    return {
        "user_id": user_id,
        "session_id": session_id,
        "litellm_messages": list(orchestration_state["litellm_messages"]),
        "litellm_model": orchestration_state["litellm_model"],
        "provider_api_key": orchestration_state["api_key"],
        "provider_api_base": ai_settings.ApiBaseUrl,
        "model_name": str(ai_settings.ModelName),
        "user_content": request.messages[-1].content if request.messages else "",
        "system_prompt": system_prompt,
        "response_format": orchestration_state["response_format"],
        "max_completion_tokens": orchestration_state["max_completion_tokens"],
        "temperature": ai_settings.Temperature,
        "top_p": ai_settings.TopP,
        "frequency_penalty": ai_settings.FrequencyPenalty,
        "presence_penalty": ai_settings.PresencePenalty,
    }
