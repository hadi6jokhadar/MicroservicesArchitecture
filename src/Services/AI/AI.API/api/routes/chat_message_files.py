from fastapi import APIRouter, Depends, Query
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, ConfigDict, UUID4
from typing import List, Optional
import uuid

from core.database import get_db
from api.dependencies import require_superadmin_or_service
from models import AiChatMessageFile

router = APIRouter()


class ChatMessageFileResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    MessageId: UUID4
    FileId: UUID4


@router.get("/", response_model=List[ChatMessageFileResponse])
async def list_chat_message_files(
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service),
    message_id: Optional[uuid.UUID] = Query(default=None, description="Filter by chat message UUID"),
    file_id: Optional[uuid.UUID] = Query(default=None, description="Filter by file UUID"),
    skip: int = Query(default=0, ge=0, description="Number of records to skip"),
    limit: int = Query(default=50, ge=1, le=500, description="Maximum number of records to return"),
):
    query = select(AiChatMessageFile)

    if message_id is not None:
        query = query.where(AiChatMessageFile.MessageId == message_id)

    if file_id is not None:
        query = query.where(AiChatMessageFile.FileId == file_id)

    query = query.offset(skip).limit(limit)

    result = await db.execute(query)
    return result.scalars().all()
