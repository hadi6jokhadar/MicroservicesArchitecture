import uuid
from datetime import datetime, timezone
from sqlalchemy import Column, String, Integer, DateTime
from sqlalchemy.dialects.postgresql import UUID
from core.database import Base

class AiTokenUsageLog(Base):
    __tablename__ = "AiTokenUsageLog"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=True, index=True)
    UserId = Column(UUID(as_uuid=True), nullable=True, index=True)
    ModelName = Column(String(100), nullable=False)
    PromptTokens = Column(Integer, nullable=False, default=0)
    CompletionTokens = Column(Integer, nullable=False, default=0)
    TotalTokens = Column(Integer, nullable=False, default=0)
    Endpoint = Column(String(255), nullable=False) # e.g. "/api/v1/chat/stream"
    CreatedAt = Column(DateTime, default=lambda: datetime.now(timezone.utc))
