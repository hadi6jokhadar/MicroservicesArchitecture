from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks
from fastapi.responses import StreamingResponse
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, UUID4
from typing import List, Optional, AsyncGenerator, Any
import json
import uuid

from core.database import get_db
from api.dependencies import require_auth, get_tenant_id
from models import AiProviderSettings, ModelTypeEnum, AiTokenUsageLog, AiChatMessage, AiChatSession

# LiteLLM unifies 100+ LLMs using the standard OpenAI format
from litellm import acompletion 

router = APIRouter()

class ChatMessage(BaseModel):
    role: str # "user", "assistant"
    content: str

class ChatRequest(BaseModel):
    session_id: Optional[UUID4] = None
    messages: List[ChatMessage]
    file_ids: Optional[List[UUID4]] = [] # For FileManager integration


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

async def log_token_usage_background(
    db: AsyncSession,
    tenant_id: str,
    user_id: str,
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int
):
    """Background task to save token usage logs without delaying the final API response"""
    usage_log = AiTokenUsageLog(
        TenantId=tenant_id,
        UserId=user_id,
        ModelName=model_name,
        Endpoint=endpoint,
        PromptTokens=prompt_tokens,
        CompletionTokens=completion_tokens,
        TotalTokens=prompt_tokens + completion_tokens
    )
    db.add(usage_log)
    await db.commit()

async def get_tenant_model_settings(db: AsyncSession, tenant_id: str, model_type: ModelTypeEnum = ModelTypeEnum.Text):
    # Try fetching tenant-specific provider
    stmt = select(AiProviderSettings).where(
        (AiProviderSettings.TenantId == tenant_id) & 
        (AiProviderSettings.ModelType == model_type)
    )
    result = await db.execute(stmt)
    setting = result.scalar_one_or_none()
    
    # Fallback to Global provider if none set for Tenant
    if not setting:
        stmt = select(AiProviderSettings).where(
            (AiProviderSettings.TenantId == None) & 
            (AiProviderSettings.ModelType == model_type)
        )
        result = await db.execute(stmt)
        setting = result.scalar_one_or_none()

    if not setting:
        raise HTTPException(status_code=500, detail="No AI Provider Settings configured.")
    return setting

@router.post("/stream")
async def chat_stream(
    request: ChatRequest,
    background_tasks: BackgroundTasks,
    tenant_id: str = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
):
    user_id = auth.get("payload", {}).get("sub", None) # Subject UUID from JWT
    
    # Fetch Custom AI Settings
    ai_settings = await get_tenant_model_settings(db, tenant_id, ModelTypeEnum.Text)

    # Format messages for LiteLLM (OpenAI standard)
    litellm_messages = [{"role": msg.role, "content": msg.content} for msg in request.messages]

    # File manager logic would typically intercept `request.file_ids` here, 
    # fetch contents from FileManagerService using httpx, and append to `litellm_messages`.
    # To keep simple here, we assume messages contain the text.

    async def generate() -> AsyncGenerator[str, None]:
        try:
            model_name_value = str(ai_settings.ModelName)
            litellm_model = build_litellm_model(ai_settings.Provider, model_name_value)

            # Connect dynamically using Tenant's Settings via LiteLLM
            response = await acompletion(
                model=litellm_model,
                messages=litellm_messages,
                api_key=ai_settings.ApiKey,
                stream=True
            )
            
            complete_response = ""
            
            async for chunk in response:
                content = chunk.choices[0].delta.content
                if content:
                    complete_response += content
                    # Server Sent Events format
                    yield f"data: {json.dumps({'content': content})}\n\n"
                    
            # Once stream is complete, we calculate assumed tokens or extract them if stream returns them in LiteLLM config
            # and fire background tracking task
            
            # Note: For strict exact streaming token counts, LiteLLM has a `stream_options={"include_usage": True}` 
            # option for OpenAI which returns usage on the final chunk. For brevity, assuming pseudo tracking here:
            background_tasks.add_task(
                log_token_usage_background,
                db, tenant_id, user_id, model_name_value, "/api/v1/chat/stream", 
                prompt_tokens=int(len(str(litellm_messages)) / 4), # Rough estimation fallback
                completion_tokens=int(len(complete_response) / 4)
            )
            
            yield "data: [DONE]\n\n"

        except Exception as e:
            yield f"data: {json.dumps({'error': str(e)})}\n\n"

    return StreamingResponse(generate(), media_type="text/event-stream")
