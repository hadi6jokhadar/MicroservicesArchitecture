from datetime import datetime
from fastapi import APIRouter, Depends, Query
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, ConfigDict, UUID4
from typing import List, Optional
import uuid

from core.database import get_db
from api.dependencies import get_tenant_id, require_superadmin_or_service
from api.attributes import optional_tenant
from models import AiTokenUsageLog

router = APIRouter()


class TokenUsageLogResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    Id: UUID4
    TenantId: Optional[str] = None
    UserId: Optional[UUID4] = None
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
    user_id: Optional[uuid.UUID] = Query(default=None, description="Filter by user UUID"),
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

    query = query.order_by(AiTokenUsageLog.CreatedAt.desc()).offset(skip).limit(limit)

    result = await db.execute(query)
    return result.scalars().all()
