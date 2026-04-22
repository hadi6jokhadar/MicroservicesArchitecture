from datetime import datetime
from fastapi import APIRouter, Depends, Query
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, ConfigDict, UUID4
from typing import List, Optional, Literal
import uuid

from core.database import get_db
from api.dependencies import require_superadmin_or_service
from models import AiChatMessage

router = APIRouter()


class ChatMessageResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    Id: UUID4
    SessionId: UUID4
    Role: str
    Content: str
    PromptTokens: int
    CompletionTokens: int
    CreatedAt: datetime


@router.get("/", response_model=List[ChatMessageResponse])
async def list_chat_messages(
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service),
    session_id: Optional[uuid.UUID] = Query(default=None, description="Filter by chat session UUID"),
    role: Optional[Literal["user", "assistant", "system", "tool"]] = Query(default=None, description="Filter by message role"),
    created_from: Optional[datetime] = Query(default=None, description="Filter messages created on or after this datetime (UTC)"),
    created_to: Optional[datetime] = Query(default=None, description="Filter messages created on or before this datetime (UTC)"),
    skip: int = Query(default=0, ge=0, description="Number of records to skip"),
    limit: int = Query(default=50, ge=1, le=500, description="Maximum number of records to return"),
):
    query = select(AiChatMessage)

    if session_id is not None:
        query = query.where(AiChatMessage.SessionId == session_id)

    if role is not None:
        query = query.where(AiChatMessage.Role == role)

    if created_from is not None:
        query = query.where(AiChatMessage.CreatedAt >= created_from)

    if created_to is not None:
        query = query.where(AiChatMessage.CreatedAt <= created_to)

    query = query.order_by(AiChatMessage.CreatedAt.asc()).offset(skip).limit(limit)

    result = await db.execute(query)
    return result.scalars().all()
