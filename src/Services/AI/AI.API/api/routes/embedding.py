from typing import Optional

import logging

from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException, status
from litellm import aembedding  # type: ignore
from sqlalchemy.ext.asyncio import AsyncSession

from api.attributes import optional_tenant
from api.dependencies import get_tenant_id, require_auth
from core.ai.db_queries import get_settings_by_key
from core.ai.persistence import schedule_token_log_task
from core.ai.schemas import EmbeddingRequest, EmbeddingResponse
from core.ai.utils import build_litellm_model, extract_user_id
from core.database import get_db
from models import ModelTypeEnum

logger = logging.getLogger(__name__)

router = APIRouter()


@router.post("", response_model=EmbeddingResponse)
@optional_tenant
async def create_embedding(
    request: EmbeddingRequest,
    background_tasks: BackgroundTasks,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth),
):
    """Generate a text embedding vector using the configured provider settings."""
    settings = await get_settings_by_key(request.settingsKey, tenant_id, db)

    if settings.ModelType != ModelTypeEnum.Embedding:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Settings key '{request.settingsKey}' is not an Embedding model (ModelType={settings.ModelType.value}).",
        )

    litellm_model = build_litellm_model(settings.Provider, settings.ModelName)

    try:
        embedding_kwargs = {
            "model": litellm_model,
            "input": request.text,
            "api_key": settings.ApiKey,
            "encoding_format": "float",
        }
        if settings.ApiBaseUrl:
            embedding_kwargs["api_base"] = settings.ApiBaseUrl

        response = await aembedding(**embedding_kwargs)  # type: ignore[assignment]
    except Exception as e:
        logger.error("Provider error during embedding: %s", e, exc_info=True)
        from core.ai.utils import map_litellm_exception_to_http
        raise map_litellm_exception_to_http(e)

    if not getattr(response, "data", None) or not response.data:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Provider returned an empty embedding response.",
        )

    raw = response.data[0]
    vector: list[float] = raw["embedding"] if isinstance(raw, dict) else raw.embedding
    model_used: str = getattr(response, "model", None) or settings.ModelName

    usage = getattr(response, "usage", None)
    prompt_tokens: int = (getattr(usage, "prompt_tokens", 0) or 0) if usage else 0

    user_id = extract_user_id(auth)
    schedule_token_log_task(
        background_tasks,
        tenant_id,
        user_id,
        settings.ModelName,
        "/api/v1/embedding",
        prompt_tokens,
        0,
    )

    return EmbeddingResponse(embedding=vector, model=model_used)
