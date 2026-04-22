from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks, status
from fastapi.responses import StreamingResponse
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from sqlalchemy import or_
from pydantic import BaseModel, Field, UUID4
from typing import List, Optional, AsyncGenerator, Any, Literal, TypedDict
import json
import uuid
from langgraph.graph import END, START, StateGraph

from core.database import get_db, AsyncSessionFactory
from api.dependencies import require_auth, get_tenant_id
from models import AiProviderSettings, AiTokenUsageLog, AiChatMessage, AiChatSession, AiSystemPrompt

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
    file_ids: List[UUID4] = Field(default_factory=list)  # For FileManager integration


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


PROVIDER_ALIASES = {
    "openai": "openai",
    "azure": "azure",
    "azureopenai": "azure",
    "anthropic": "anthropic",
    "google": "gemini",
    "gemini": "gemini",
    "ollama": "ollama",
    "groq": "groq",
    "mistral": "mistral",
}


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
    tenant_id: str,
    user_id: Optional[str],
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int,
):
    """Background task: opens its own DB session to avoid using a closed request-scoped session."""
    async with AsyncSessionFactory() as db:
        usage_log = AiTokenUsageLog(
            TenantId=tenant_id,
            UserId=uuid.UUID(user_id) if user_id else None,
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
    tenant_id: str,
    db: AsyncSession,
) -> AiProviderSettings:
    """Lookup AiProviderSettings by Key, scoped to the caller's tenant or global (TenantId IS NULL)."""
    stmt = select(AiProviderSettings).where(
        AiProviderSettings.Key == key,
        or_(AiProviderSettings.TenantId == tenant_id, AiProviderSettings.TenantId.is_(None)),
    ).order_by(
        # Prefer tenant-specific over global when both share the same key
        AiProviderSettings.TenantId.is_(None)
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
    tenant_id: str,
    db: AsyncSession,
) -> Optional[str]:
    """Lookup AiSystemPrompt by Name, scoped to the caller's tenant or global. Returns prompt text or None."""
    stmt = select(AiSystemPrompt).where(
        AiSystemPrompt.Name == name,
        or_(AiSystemPrompt.TenantId == tenant_id, AiSystemPrompt.TenantId.is_(None)),
    ).order_by(
        # Prefer tenant-specific over global when both share the same name
        AiSystemPrompt.TenantId.is_(None)
    )
    result = await db.execute(stmt)
    prompt = result.scalars().first()
    return prompt.PromptText if prompt else None

@router.post("/stream")
async def chat_stream(
    request: ChatRequest,
    background_tasks: BackgroundTasks,
    tenant_id: str = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
):
    user_id: Optional[str] = auth.get("payload", {}).get("sub", None)  # Subject UUID from JWT

    # 1. Resolve provider settings by key (tenant-scoped, global fallback)
    ai_settings = await get_settings_by_key(request.settings_key, tenant_id, db)

    # 2. Resolve system prompt by key if provided (tenant-scoped, global fallback)
    system_prompt: Optional[str] = None
    if request.system_prompt_key:
        system_prompt = await get_system_prompt_by_key(request.system_prompt_key, tenant_id, db)

    # 3. Resolve or create the chat session
    session_id: uuid.UUID
    if request.session_id:
        stmt = select(AiChatSession).where(
            AiChatSession.Id == request.session_id,
            AiChatSession.TenantId == tenant_id,
        )
        result = await db.execute(stmt)
        session = result.scalar_one_or_none()
        if session is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Chat session not found.",
            )
        session_id = session.Id
    else:
        new_session = AiChatSession(
            TenantId=tenant_id,
            UserId=uuid.UUID(user_id) if user_id else uuid.uuid4(),
        )
        db.add(new_session)
        await db.flush()   # populates new_session.Id without committing yet
        session_id = new_session.Id
        await db.commit()

    orchestration_state = await run_chat_orchestration(request, ai_settings, system_prompt)
    litellm_messages = orchestration_state["litellm_messages"]
    litellm_model = orchestration_state["litellm_model"]
    provider_api_key = orchestration_state["api_key"]
    model_name_value = str(ai_settings.ModelName)

    # Capture the last user message content for persistence
    user_content = request.messages[-1].content

    async def generate() -> AsyncGenerator[str, None]:
        complete_response = ""
        prompt_tokens = 0
        completion_tokens = 0
        success = False

        try:
            # Connect dynamically using resolved settings via LiteLLM
            response = await acompletion(
                model=litellm_model,
                messages=litellm_messages,
                api_key=provider_api_key,
                stream=True,
                stream_options={"include_usage": True},
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
                # Fallback token estimation if provider didn't return usage
                if prompt_tokens == 0:
                    prompt_tokens = max(1, len(str(litellm_messages)) // 4)
                if completion_tokens == 0:
                    completion_tokens = max(1, len(complete_response) // 4)

                # Persist user + assistant messages
                background_tasks.add_task(
                    persist_messages_background,
                    session_id,
                    user_content,
                    complete_response,
                    prompt_tokens,
                    completion_tokens,
                )

                # Persist token usage log
                background_tasks.add_task(
                    log_token_usage_background,
                    tenant_id,
                    user_id,
                    model_name_value,
                    "/api/v1/chat/stream",
                    prompt_tokens,
                    completion_tokens,
                )

    return StreamingResponse(generate(), media_type="text/event-stream")
