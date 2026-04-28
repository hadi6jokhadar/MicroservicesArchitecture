import uuid
from typing import List, Optional, TypedDict

from fastapi import HTTPException, status
from langgraph.graph import END, START, StateGraph
from sqlalchemy.ext.asyncio import AsyncSession

from core.ai.db_queries import get_settings_by_key, get_system_prompt_by_key
from core.ai.file_context import inject_file_context_if_present
from core.ai.schemas import ChatMessage, ChatRequest
from core.ai.sessions import resolve_or_create_session
from core.ai.utils import (
    build_litellm_model,
    extract_user_id,
    normalize_model_type,
    parse_response_format,
)
from models import AiProviderSettings, ModelTypeEnum


# ---------------------------------------------------------------------------
# LangGraph state
# ---------------------------------------------------------------------------

class ChatWorkflowState(TypedDict):
    request: ChatRequest
    provider: str
    model_name: str
    api_key: str
    system_prompt: Optional[str]
    response_format: Optional[dict]
    litellm_messages: List[dict[str, str]]
    litellm_model: str
    max_completion_tokens: Optional[int]


# ---------------------------------------------------------------------------
# Runtime context returned to the route handler
# ---------------------------------------------------------------------------

class ChatRuntimeContext(TypedDict):
    user_id: Optional[int]
    session_id: uuid.UUID
    litellm_messages: List[dict[str, str]]
    litellm_model: str
    provider_api_key: str
    provider_api_base: Optional[str]
    model_name: str
    user_content: str
    response_format: Optional[dict]
    max_completion_tokens: Optional[int]
    temperature: Optional[float]
    top_p: Optional[float]
    frequency_penalty: Optional[float]
    presence_penalty: Optional[float]


# ---------------------------------------------------------------------------
# Workflow nodes
# ---------------------------------------------------------------------------

def _prepare_messages_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Build the LiteLLM message list, prepending the system prompt when present."""
    messages: List[dict[str, str]] = []
    if state["system_prompt"]:
        messages.append({"role": "system", "content": state["system_prompt"]})
    messages.extend(
        {"role": msg.role, "content": msg.content}
        for msg in state["request"].messages
    )
    return {**state, "litellm_messages": messages}


def _resolve_model_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Resolve the fully-qualified LiteLLM model identifier."""
    return {
        **state,
        "litellm_model": build_litellm_model(state["provider"], state["model_name"]),
    }


# ---------------------------------------------------------------------------
# Compiled graph (module-level singleton)
# ---------------------------------------------------------------------------

def _build_chat_workflow():
    workflow = StateGraph(ChatWorkflowState)
    workflow.add_node("prepare_messages", _prepare_messages_node)
    workflow.add_node("resolve_model", _resolve_model_node)
    workflow.add_edge(START, "prepare_messages")
    workflow.add_edge("prepare_messages", "resolve_model")
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
) -> ChatWorkflowState:
    """Run the LangGraph chat workflow and return the final state."""
    initial_state: ChatWorkflowState = {
        "request": request,
        "provider": str(ai_settings.Provider),
        "model_name": str(ai_settings.ModelName),
        "api_key": str(ai_settings.ApiKey),
        "system_prompt": system_prompt,
        "response_format": response_format,
        "litellm_messages": [],
        "litellm_model": "",
        "max_completion_tokens": max_completion_tokens,
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

    if normalize_model_type(ai_settings) == ModelTypeEnum.Audio.value.lower():
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=(
                f"Provider setting '{request.settings_key}' is configured for audio transcription "
                "(ModelType=Audio). Use POST /api/v1/asr/transcribe instead."
            ),
        )

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
        request, ai_settings, system_prompt, response_format, max_completion_tokens
    )

    litellm_messages: List[dict[str, str]] = list(orchestration_state["litellm_messages"])
    litellm_messages = await inject_file_context_if_present(
        litellm_messages, request.file_ids, tenant_id
    )

    return {
        "user_id": user_id,
        "session_id": session_id,
        "litellm_messages": litellm_messages,
        "litellm_model": orchestration_state["litellm_model"],
        "provider_api_key": orchestration_state["api_key"],
        "provider_api_base": ai_settings.ApiBaseUrl,
        "model_name": str(ai_settings.ModelName),
        "user_content": request.messages[-1].content,
        "response_format": orchestration_state["response_format"],
        "max_completion_tokens": orchestration_state["max_completion_tokens"],
        "temperature": ai_settings.Temperature,
        "top_p": ai_settings.TopP,
        "frequency_penalty": ai_settings.FrequencyPenalty,
        "presence_penalty": ai_settings.PresencePenalty,
    }
