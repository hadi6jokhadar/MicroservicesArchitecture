from datetime import datetime
from fastapi import APIRouter, Depends, Query
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from sqlalchemy import func, cast, Date
from pydantic import BaseModel, ConfigDict, UUID4
from typing import List, Optional
import uuid

from core.database import get_db
from api.dependencies import get_tenant_id, require_superadmin_or_service
from api.attributes import optional_tenant
from models import AiTokenUsageLog

router = APIRouter()


# ---------------------------------------------------------------------------
# Stats response models
# ---------------------------------------------------------------------------


class TokensByModelItem(BaseModel):
    model_name: str
    total_tokens: int
    prompt_tokens: int
    completion_tokens: int
    request_count: int


class TokensByEndpointItem(BaseModel):
    endpoint: str
    total_tokens: int
    request_count: int


class TokensOverTimeItem(BaseModel):
    date: str
    total_tokens: int
    request_count: int


class TokenUsageStatsResponse(BaseModel):
    total_tokens: int
    prompt_tokens: int
    completion_tokens: int
    total_requests: int
    avg_tokens_per_request: float
    tokens_by_model: List[TokensByModelItem]
    tokens_by_endpoint: List[TokensByEndpointItem]
    tokens_over_time: List[TokensOverTimeItem]


class TokenUsageLogResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    Id: UUID4
    TenantId: Optional[str] = None
    UserId: Optional[int] = None
    ModelName: str
    PromptTokens: int
    CompletionTokens: int
    TotalTokens: int
    Endpoint: str
    CreatedAt: datetime


@router.get("/", response_model=List[TokenUsageLogResponse])
@optional_tenant
async def list_token_usage_logs(
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service),
    user_id: Optional[int] = Query(default=None, description="Filter by user ID"),
    model_name: Optional[str] = Query(default=None, description="Filter by model name (case-insensitive substring)"),
    endpoint: Optional[str] = Query(default=None, description="Filter by endpoint path (case-insensitive substring)"),
    created_from: Optional[datetime] = Query(default=None, description="Filter logs created on or after this datetime (UTC)"),
    created_to: Optional[datetime] = Query(default=None, description="Filter logs created on or before this datetime (UTC)"),
    skip: int = Query(default=0, ge=0, description="Number of records to skip"),
    limit: int = Query(default=50, ge=1, le=500, description="Maximum number of records to return"),
):
    query = select(AiTokenUsageLog)

    if tenant_id:
        query = query.where(AiTokenUsageLog.TenantId == tenant_id)

    if user_id is not None:
        query = query.where(AiTokenUsageLog.UserId == user_id)

    if model_name is not None:
        query = query.where(AiTokenUsageLog.ModelName.ilike(f"%{model_name}%"))

    if endpoint is not None:
        query = query.where(AiTokenUsageLog.Endpoint.ilike(f"%{endpoint}%"))

    if created_from is not None:
        query = query.where(AiTokenUsageLog.CreatedAt >= created_from)

    if created_to is not None:
        query = query.where(AiTokenUsageLog.CreatedAt <= created_to)

    query = query.order_by(AiTokenUsageLog.CreatedAt.desc(), AiTokenUsageLog.Id.desc()).offset(skip).limit(limit)

    result = await db.execute(query)
    return result.scalars().all()


@router.get("/stats", response_model=TokenUsageStatsResponse)
@optional_tenant
async def get_token_usage_stats(
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service),
    model_name: Optional[str] = Query(default=None, description="Filter by model name (case-insensitive substring)"),
    endpoint: Optional[str] = Query(default=None, description="Filter by endpoint path (case-insensitive substring)"),
    created_from: Optional[datetime] = Query(default=None, description="Filter logs created on or after this datetime (UTC)"),
    created_to: Optional[datetime] = Query(default=None, description="Filter logs created on or before this datetime (UTC)"),
):
    def _apply_filters(q):
        if tenant_id:
            q = q.where(AiTokenUsageLog.TenantId == tenant_id)
        if model_name is not None:
            q = q.where(AiTokenUsageLog.ModelName.ilike(f"%{model_name}%"))
        if endpoint is not None:
            q = q.where(AiTokenUsageLog.Endpoint.ilike(f"%{endpoint}%"))
        if created_from is not None:
            q = q.where(AiTokenUsageLog.CreatedAt >= created_from)
        if created_to is not None:
            q = q.where(AiTokenUsageLog.CreatedAt <= created_to)
        return q

    # --- summary totals ---
    totals_query = _apply_filters(
        select(
            func.coalesce(func.sum(AiTokenUsageLog.TotalTokens), 0).label("total_tokens"),
            func.coalesce(func.sum(AiTokenUsageLog.PromptTokens), 0).label("prompt_tokens"),
            func.coalesce(func.sum(AiTokenUsageLog.CompletionTokens), 0).label("completion_tokens"),
            func.count(AiTokenUsageLog.Id).label("total_requests"),
        )
    )
    totals_result = await db.execute(totals_query)
    totals = totals_result.one()

    total_tokens = int(totals.total_tokens)
    prompt_tokens = int(totals.prompt_tokens)
    completion_tokens = int(totals.completion_tokens)
    total_requests = int(totals.total_requests)
    avg_tokens = round(total_tokens / total_requests, 2) if total_requests > 0 else 0.0

    # --- tokens by model ---
    by_model_query = _apply_filters(
        select(
            AiTokenUsageLog.ModelName.label("model_name"),
            func.sum(AiTokenUsageLog.TotalTokens).label("total_tokens"),
            func.sum(AiTokenUsageLog.PromptTokens).label("prompt_tokens"),
            func.sum(AiTokenUsageLog.CompletionTokens).label("completion_tokens"),
            func.count(AiTokenUsageLog.Id).label("request_count"),
        ).group_by(AiTokenUsageLog.ModelName).order_by(func.sum(AiTokenUsageLog.TotalTokens).desc())
    )
    by_model_result = await db.execute(by_model_query)
    tokens_by_model = [
        TokensByModelItem(
            model_name=row.model_name,
            total_tokens=int(row.total_tokens),
            prompt_tokens=int(row.prompt_tokens),
            completion_tokens=int(row.completion_tokens),
            request_count=int(row.request_count),
        )
        for row in by_model_result.all()
    ]

    # --- tokens by endpoint ---
    by_endpoint_query = _apply_filters(
        select(
            AiTokenUsageLog.Endpoint.label("endpoint"),
            func.sum(AiTokenUsageLog.TotalTokens).label("total_tokens"),
            func.count(AiTokenUsageLog.Id).label("request_count"),
        ).group_by(AiTokenUsageLog.Endpoint).order_by(func.sum(AiTokenUsageLog.TotalTokens).desc())
    )
    by_endpoint_result = await db.execute(by_endpoint_query)
    tokens_by_endpoint = [
        TokensByEndpointItem(
            endpoint=row.endpoint,
            total_tokens=int(row.total_tokens),
            request_count=int(row.request_count),
        )
        for row in by_endpoint_result.all()
    ]

    # --- tokens over time (daily) ---
    over_time_query = _apply_filters(
        select(
            cast(AiTokenUsageLog.CreatedAt, Date).label("date"),
            func.sum(AiTokenUsageLog.TotalTokens).label("total_tokens"),
            func.count(AiTokenUsageLog.Id).label("request_count"),
        ).group_by(cast(AiTokenUsageLog.CreatedAt, Date)).order_by(cast(AiTokenUsageLog.CreatedAt, Date))
    )
    over_time_result = await db.execute(over_time_query)
    tokens_over_time = [
        TokensOverTimeItem(
            date=str(row.date),
            total_tokens=int(row.total_tokens),
            request_count=int(row.request_count),
        )
        for row in over_time_result.all()
    ]

    return TokenUsageStatsResponse(
        total_tokens=total_tokens,
        prompt_tokens=prompt_tokens,
        completion_tokens=completion_tokens,
        total_requests=total_requests,
        avg_tokens_per_request=avg_tokens,
        tokens_by_model=tokens_by_model,
        tokens_by_endpoint=tokens_by_endpoint,
        tokens_over_time=tokens_over_time,
    )
