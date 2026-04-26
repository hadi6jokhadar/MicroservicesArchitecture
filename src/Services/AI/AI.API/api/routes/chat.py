from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks, status
from fastapi.responses import StreamingResponse
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from sqlalchemy import or_
from pydantic import BaseModel, Field, UUID4
from typing import AsyncIterator, List, Optional, AsyncGenerator, Any, Literal, TypedDict
import json
import uuid
from langgraph.graph import END, START, StateGraph

from core.database import get_db, AsyncSessionFactory
from api.dependencies import require_auth, get_tenant_id
from api.attributes import optional_tenant
from models import AiProviderSettings, AiTokenUsageLog, AiChatMessage, AiChatSession, AiSystemPrompt
from ihsandev_shared.clients import FileManagerServiceClient
from core.config import settings

file_manager_client = FileManagerServiceClient(
    base_url=settings.FileManagerSettings.BaseUrl,
    shared_secret=settings.ServiceCommunication.SharedSecret,
    service_name=settings.ServiceCommunication.ServiceName,
)

# LiteLLM unifies 100+ LLMs using the standard OpenAI format
from litellm import acompletion  # type: ignore

router = APIRouter()

class ChatMessage(BaseModel):
    role: Literal["system", "user", "assistant", "tool"]
    content: str = Field(min_length=1)

class ChatRequest(BaseModel):
    session_id: Optional[UUID4] = None
    settings_key: str = Field(min_length=1)               # Resolves AiProviderSettings by Key
    system_prompt_key: Optional[str] = None               # Resolves AiSystemPrompt by Name (optional)
    messages: List[ChatMessage] = Field(min_length=1)
    file_ids: List[int] = Field(default_factory=list)  # FileManager integer IDs for context injection


class ChatSingleResponse(BaseModel):
    session_id: UUID4
    content: str
    prompt_tokens: int
    completion_tokens: int
    total_tokens: int


class ChatOrchestrationInput(BaseModel):
    request: ChatRequest
    provider: str = Field(min_length=1)
    model_name: str = Field(min_length=1)
    api_key: str = Field(min_length=1)
    system_prompt: Optional[str] = None


class ChatWorkflowState(TypedDict):
    request: ChatRequest
    provider: str
    model_name: str
    api_key: str
    system_prompt: Optional[str]
    litellm_messages: List[dict[str, str]]
    litellm_model: str


class ChatRuntimeContext(TypedDict):
    user_id: Optional[int]
    session_id: uuid.UUID
    litellm_messages: List[dict[str, str]]
    litellm_model: str
    provider_api_key: str
    provider_api_base: Optional[str]
    model_name: str
    user_content: str


PROVIDER_ALIASES = {
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

GLOBAL_CHAT_TENANT_ID = "global"
QWEN_PROVIDER_NAMES = {"qwen", "qwenai"}


def build_litellm_model(provider: Any, model_name: Any) -> str:
    """Build a LiteLLM model identifier from DB values in a case-insensitive way."""
    provider_text = str(provider or "")
    model_text = str(model_name or "")

    normalized_provider = PROVIDER_ALIASES.get(provider_text.strip().lower(), provider_text.strip().lower())
    normalized_model = model_text.strip()

    if not normalized_provider:
        return normalized_model

    # If model is already provider-qualified, keep it unchanged to avoid double-prefixing.
    if "/" in normalized_model:
        return normalized_model

    return f"{normalized_provider}/{normalized_model}"


def _resolve_provider_api_base(provider: Any) -> Optional[str]:
    provider_key = str(provider or "").strip().lower()
    if provider_key in QWEN_PROVIDER_NAMES:
        return settings.AiProviderRouting.QwenOpenAiCompatibleBaseUrl
    return None


def _prepare_messages_node(state: ChatWorkflowState) -> ChatWorkflowState:
    """Convert validated Pydantic messages into LiteLLM payload format, prepending system prompt if present."""
    messages: List[dict[str, str]] = []
    if state["system_prompt"]:
        messages.append({"role": "system", "content": state["system_prompt"]})
    messages.extend({"role": msg.role, "content": msg.content} for msg in state["request"].messages)
    return {
        **state,
        "litellm_messages": messages,
    }


def _resolve_model_node(state: ChatWorkflowState) -> ChatWorkflowState:
    return {
        **state,
        "litellm_model": build_litellm_model(state["provider"], state["model_name"]),
    }


def _build_chat_workflow():
    workflow = StateGraph(ChatWorkflowState)
    workflow.add_node("prepare_messages", _prepare_messages_node)
    workflow.add_node("resolve_model", _resolve_model_node)
    workflow.add_edge(START, "prepare_messages")
    workflow.add_edge("prepare_messages", "resolve_model")
    workflow.add_edge("resolve_model", END)
    return workflow.compile()


CHAT_WORKFLOW = _build_chat_workflow()


async def run_chat_orchestration(
    request: ChatRequest,
    ai_settings: AiProviderSettings,
    system_prompt: Optional[str] = None,
) -> ChatWorkflowState:
    orchestration_input = ChatOrchestrationInput(
        request=request,
        provider=str(ai_settings.Provider),
        model_name=str(ai_settings.ModelName),
        api_key=str(ai_settings.ApiKey),
        system_prompt=system_prompt,
    )

    initial_state: ChatWorkflowState = {
        "request": orchestration_input.request,
        "provider": orchestration_input.provider,
        "model_name": orchestration_input.model_name,
        "api_key": orchestration_input.api_key,
        "system_prompt": orchestration_input.system_prompt,
        "litellm_messages": [],
        "litellm_model": "",
    }

    return await CHAT_WORKFLOW.ainvoke(initial_state) # type: ignore

async def log_token_usage_background(
    tenant_id: Optional[str],
    user_id: Optional[int],
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int,
):
    """Background task: opens its own DB session to avoid using a closed request-scoped session."""
    async with AsyncSessionFactory() as db:
        usage_log = AiTokenUsageLog(
            TenantId=tenant_id,
            UserId=user_id,
            ModelName=model_name,
            Endpoint=endpoint,
            PromptTokens=prompt_tokens,
            CompletionTokens=completion_tokens,
            TotalTokens=prompt_tokens + completion_tokens,
        )
        db.add(usage_log)
        await db.commit()


async def persist_messages_background(
    session_id: uuid.UUID,
    user_content: str,
    assistant_content: str,
    prompt_tokens: int,
    completion_tokens: int,
):
    """Background task: persists the user message and the assistant reply to AiChatMessage."""
    async with AsyncSessionFactory() as db:
        user_msg = AiChatMessage(
            SessionId=session_id,
            Role="user",
            Content=user_content,
            PromptTokens=prompt_tokens,
            CompletionTokens=0,
        )
        assistant_msg = AiChatMessage(
            SessionId=session_id,
            Role="assistant",
            Content=assistant_content,
            PromptTokens=0,
            CompletionTokens=completion_tokens,
        )
        db.add(user_msg)
        db.add(assistant_msg)
        await db.commit()

async def get_settings_by_key(
    key: str,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> AiProviderSettings:
    """Lookup AiProviderSettings by Key.

    With tenant context: prefer tenant row, then global fallback.
    Without tenant context: resolve by key regardless of TenantId.
    """
    if tenant_id:
        stmt = select(AiProviderSettings).where(
            AiProviderSettings.Key == key,
            or_(AiProviderSettings.TenantId == tenant_id, AiProviderSettings.TenantId.is_(None)),
        ).order_by(
            # Prefer tenant-specific over global when both share the same key
            AiProviderSettings.TenantId.is_(None)
        )
    else:
        stmt = select(AiProviderSettings).where(
            AiProviderSettings.Key == key,
        )
    result = await db.execute(stmt)
    setting = result.scalars().first()
    if not setting:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"AI provider setting '{key}' not found.",
        )
    return setting


async def get_system_prompt_by_key(
    name: str,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> Optional[str]:
    """Lookup AiSystemPrompt by Name, scoped to the caller's tenant or global. Returns prompt text or None."""
    if tenant_id:
        stmt = select(AiSystemPrompt).where(
            AiSystemPrompt.Name == name,
            or_(AiSystemPrompt.TenantId == tenant_id, AiSystemPrompt.TenantId.is_(None)),
        ).order_by(
            # Prefer tenant-specific over global when both share the same name
            AiSystemPrompt.TenantId.is_(None)
        )
    else:
        stmt = select(AiSystemPrompt).where(
            AiSystemPrompt.Name == name,
            AiSystemPrompt.TenantId.is_(None),
        )
    result = await db.execute(stmt)
    prompt = result.scalars().first()
    return prompt.PromptText if prompt else None


def _extract_user_id(auth: dict) -> Optional[int]:
    """Extract integer user id from JWT payload when available."""
    user_id_str: Optional[str] = auth.get("payload", {}).get("nameid", None)
    return int(user_id_str) if user_id_str and user_id_str.isdigit() else None


async def _resolve_system_prompt_for_request(
    request: ChatRequest,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> Optional[str]:
    if not request.system_prompt_key:
        return None
    return await get_system_prompt_by_key(request.system_prompt_key, tenant_id, db)


async def _resolve_or_create_session_id(
    requested_session_id: Optional[UUID4],
    tenant_id: Optional[str],
    user_id: Optional[int],
    db: AsyncSession,
) -> uuid.UUID:
    session_tenant_id = tenant_id or GLOBAL_CHAT_TENANT_ID

    if requested_session_id:
        stmt = select(AiChatSession).where(
            AiChatSession.Id == requested_session_id,
            AiChatSession.TenantId == session_tenant_id,
        )
        result = await db.execute(stmt)
        session = result.scalar_one_or_none()
        if session is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Chat session not found.",
            )
        return session.Id

    # If no user_id found in JWT, use 0 as UserId is NOT NULL in AiChatSession.
    new_session = AiChatSession(
        TenantId=session_tenant_id,
        UserId=user_id or 0,
    )
    db.add(new_session)
    await db.flush()  # populates new_session.Id without committing yet
    if new_session.Id is None:
        new_session.Id = uuid.uuid4()
    await db.commit()
    return new_session.Id


def _build_file_context_message(files_metadata: Any) -> Optional[dict[str, str]]:
    if not files_metadata:
        return None

    file_lines = [
        f"- {f.get('name', 'file')}{f.get('extension', '')} → {f.get('url', '')}"
        for f in files_metadata
        if isinstance(f, dict) and f.get("url")
    ]
    if not file_lines:
        return None

    return {
        "role": "user",
        "content": "The following files are attached to this message:\n" + "\n".join(file_lines),
    }


async def _inject_file_context_if_present(
    litellm_messages: List[dict[str, str]],
    file_ids: List[int],
    tenant_id: Optional[str],
) -> List[dict[str, str]]:
    if not file_ids:
        return litellm_messages

    files_metadata = await file_manager_client.get_files_by_ids(file_ids, tenant_id)
    file_context = _build_file_context_message(files_metadata)
    if not file_context:
        return litellm_messages

    # Inject file context immediately before the last user message.
    return litellm_messages[:-1] + [file_context] + litellm_messages[-1:]


def _estimate_tokens_if_missing(
    prompt_tokens: int,
    completion_tokens: int,
    litellm_messages: List[dict[str, str]],
    complete_response: str,
) -> tuple[int, int]:
    estimated_prompt_tokens = prompt_tokens
    estimated_completion_tokens = completion_tokens

    if estimated_prompt_tokens == 0:
        estimated_prompt_tokens = max(1, len(str(litellm_messages)) // 4)
    if estimated_completion_tokens == 0:
        estimated_completion_tokens = max(1, len(complete_response) // 4)

    return estimated_prompt_tokens, estimated_completion_tokens


def _schedule_chat_persistence_tasks(
    background_tasks: BackgroundTasks,
    session_id: uuid.UUID,
    user_content: str,
    complete_response: str,
    tenant_id: Optional[str],
    user_id: Optional[int],
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int,
) -> None:
    background_tasks.add_task(
        persist_messages_background,
        session_id,
        user_content,
        complete_response,
        prompt_tokens,
        completion_tokens,
    )

    background_tasks.add_task(
        log_token_usage_background,
        tenant_id,
        user_id,
        model_name,
        endpoint,
        prompt_tokens,
        completion_tokens,
    )


async def _build_chat_runtime_context(
    request: ChatRequest,
    tenant_id: Optional[str],
    db: AsyncSession,
    auth: dict,
) -> ChatRuntimeContext:
    user_id = _extract_user_id(auth)
    ai_settings = await get_settings_by_key(request.settings_key, tenant_id, db)
    provider_api_base = _resolve_provider_api_base(ai_settings.Provider)
    system_prompt = await _resolve_system_prompt_for_request(request, tenant_id, db)
    session_id = await _resolve_or_create_session_id(request.session_id, tenant_id, user_id, db)

    orchestration_state = await run_chat_orchestration(request, ai_settings, system_prompt)
    litellm_messages: List[dict[str, str]] = list(orchestration_state["litellm_messages"])
    litellm_messages = await _inject_file_context_if_present(litellm_messages, request.file_ids, tenant_id)

    return {
        "user_id": user_id,
        "session_id": session_id,
        "litellm_messages": litellm_messages,
        "litellm_model": orchestration_state["litellm_model"],
        "provider_api_key": orchestration_state["api_key"],
        "provider_api_base": provider_api_base,
        "model_name": str(ai_settings.ModelName),
        "user_content": request.messages[-1].content,
    }


def _extract_non_stream_content(response: Any) -> str:
    if not getattr(response, "choices", None):
        return ""

    first_choice = response.choices[0]
    message = getattr(first_choice, "message", None)
    if message is None:
        return ""

    content = getattr(message, "content", "")
    return content or ""


def _extract_usage_tokens(response: Any) -> tuple[int, int]:
    usage = getattr(response, "usage", None)
    if usage is None:
        return 0, 0

    prompt_tokens = getattr(usage, "prompt_tokens", 0) or 0
    completion_tokens = getattr(usage, "completion_tokens", 0) or 0
    return prompt_tokens, completion_tokens

@router.post("/stream")
@optional_tenant
async def chat_stream(
    request: ChatRequest,
    background_tasks: BackgroundTasks,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
):
    runtime_context = await _build_chat_runtime_context(request, tenant_id, db, auth)

    session_id = runtime_context["session_id"]
    litellm_messages = runtime_context["litellm_messages"]
    litellm_model = runtime_context["litellm_model"]
    provider_api_key = runtime_context["provider_api_key"]
    provider_api_base = runtime_context["provider_api_base"]
    model_name_value = runtime_context["model_name"]
    user_id = runtime_context["user_id"]
    user_content = runtime_context["user_content"]

    async def generate() -> AsyncGenerator[str, None]:
        complete_response = ""
        prompt_tokens = 0
        completion_tokens = 0
        success = False

        try:
            # Connect dynamically using resolved settings via LiteLLM
            completion_kwargs: dict[str, Any] = {
                "model": litellm_model,
                "messages": litellm_messages,
                "api_key": provider_api_key,
                "stream": True,
                "stream_options": {"include_usage": True},
            }
            if provider_api_base:
                completion_kwargs["api_base"] = provider_api_base

            response: AsyncIterator[Any] = await acompletion(  # type: ignore[assignment]
                **completion_kwargs
            )

            async for chunk in response:
                # Extract usage from the final chunk when available (OpenAI-compatible providers)
                if hasattr(chunk, "usage") and chunk.usage:
                    prompt_tokens = chunk.usage.prompt_tokens or 0
                    completion_tokens = chunk.usage.completion_tokens or 0

                delta_content = chunk.choices[0].delta.content if chunk.choices else None
                if delta_content:
                    complete_response += delta_content
                    yield f"data: {json.dumps({'session_id': str(session_id), 'content': delta_content})}\n\n"

            success = True
            yield "data: [DONE]\n\n"

        except Exception as e:
            yield f"data: {json.dumps({'error': str(e)})}\n\n"

        finally:
            if success:
                prompt_tokens, completion_tokens = _estimate_tokens_if_missing(
                    prompt_tokens,
                    completion_tokens,
                    litellm_messages,
                    complete_response,
                )

                _schedule_chat_persistence_tasks(
                    background_tasks,
                    session_id,
                    user_content,
                    complete_response,
                    tenant_id,
                    user_id,
                    model_name_value,
                    "/api/v1/chat/stream",
                    prompt_tokens,
                    completion_tokens,
                )

    return StreamingResponse(generate(), media_type="text/event-stream")


@router.post("/single", response_model=ChatSingleResponse)
@optional_tenant
async def chat_single_response(
    request: ChatRequest,
    background_tasks: BackgroundTasks,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
):
    runtime_context = await _build_chat_runtime_context(request, tenant_id, db, auth)

    session_id = runtime_context["session_id"]
    litellm_messages = runtime_context["litellm_messages"]
    litellm_model = runtime_context["litellm_model"]
    provider_api_key = runtime_context["provider_api_key"]
    provider_api_base = runtime_context["provider_api_base"]
    model_name_value = runtime_context["model_name"]
    user_id = runtime_context["user_id"]
    user_content = runtime_context["user_content"]

    try:
        completion_kwargs: dict[str, Any] = {
            "model": litellm_model,
            "messages": litellm_messages,
            "api_key": provider_api_key,
            "stream": False,
        }
        if provider_api_base:
            completion_kwargs["api_base"] = provider_api_base

        response = await acompletion(**completion_kwargs)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=str(e),
        )

    assistant_content = _extract_non_stream_content(response)
    prompt_tokens, completion_tokens = _extract_usage_tokens(response)
    prompt_tokens, completion_tokens = _estimate_tokens_if_missing(
        prompt_tokens,
        completion_tokens,
        litellm_messages,
        assistant_content,
    )

    _schedule_chat_persistence_tasks(
        background_tasks,
        session_id,
        user_content,
        assistant_content,
        tenant_id,
        user_id,
        model_name_value,
        "/api/v1/chat/single",
        prompt_tokens,
        completion_tokens,
    )

    return ChatSingleResponse(
        session_id=session_id,
        content=assistant_content,
        prompt_tokens=prompt_tokens,
        completion_tokens=completion_tokens,
        total_tokens=prompt_tokens + completion_tokens,
    )
