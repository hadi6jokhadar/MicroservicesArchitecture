from datetime import datetime
from fastapi import APIRouter, Depends, Query, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, ConfigDict, UUID4
from typing import List, Optional
import uuid

from core.database import get_db
from api.dependencies import get_tenant_id, require_superadmin_or_service, require_auth
from api.attributes import optional_tenant
from models import AiChatSession

router = APIRouter()


class ChatSessionResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    Id: UUID4
    TenantId: str
    UserId: int
    Title: Optional[str] = None
    CreatedAt: datetime


class UpdateChatSessionRequest(BaseModel):
    title: str


@router.get("/", response_model=List[ChatSessionResponse])
@optional_tenant
async def list_chat_sessions(
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service),
    user_id: Optional[int] = Query(default=None, description="Filter by user ID"),
    title: Optional[str] = Query(default=None, description="Filter by title (case-insensitive substring)"),
    created_from: Optional[datetime] = Query(default=None, description="Filter sessions created on or after this datetime (UTC)"),
    created_to: Optional[datetime] = Query(default=None, description="Filter sessions created on or before this datetime (UTC)"),
    skip: int = Query(default=0, ge=0, description="Number of records to skip"),
    limit: int = Query(default=50, ge=1, le=500, description="Maximum number of records to return"),
):
    query = select(AiChatSession)

    if tenant_id:
        query = query.where(AiChatSession.TenantId == tenant_id)

    if user_id is not None:
        query = query.where(AiChatSession.UserId == user_id)

    if title is not None:
        query = query.where(AiChatSession.Title.ilike(f"%{title}%"))

    if created_from is not None:
        query = query.where(AiChatSession.CreatedAt >= created_from)

    if created_to is not None:
        query = query.where(AiChatSession.CreatedAt <= created_to)

    query = query.order_by(AiChatSession.CreatedAt.desc(), AiChatSession.Id.desc()).offset(skip).limit(limit)

    result = await db.execute(query)
    return result.scalars().all()


@router.delete("/{session_id}", status_code=204)
@optional_tenant
async def delete_chat_session(
    session_id: UUID4,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service),
):
    query = select(AiChatSession).where(AiChatSession.Id == session_id)
    if tenant_id:
        query = query.where(AiChatSession.TenantId == tenant_id)

    result = await db.execute(query)
    session = result.scalar_one_or_none()

    if session is None:
        raise HTTPException(status_code=404, detail="Chat session not found")

    await db.delete(session)
    await db.commit()


@router.patch("/{session_id}", response_model=ChatSessionResponse)
@optional_tenant
async def update_chat_session(
    session_id: UUID4,
    body: UpdateChatSessionRequest,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service),
):
    query = select(AiChatSession).where(AiChatSession.Id == session_id)
    if tenant_id:
        query = query.where(AiChatSession.TenantId == tenant_id)

    result = await db.execute(query)
    session = result.scalar_one_or_none()

    if session is None:
        raise HTTPException(status_code=404, detail="Chat session not found")

    session.Title = body.title
    await db.commit()
    await db.refresh(session)
    return session

