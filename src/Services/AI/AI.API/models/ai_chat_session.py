import uuid
from datetime import datetime, timezone
from sqlalchemy import Column, String, DateTime
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship
from core.database import Base

class AiChatSession(Base):
    __tablename__ = "AiChatSession"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=False, index=True)
    UserId = Column(UUID(as_uuid=True), nullable=False, index=True)
    Title = Column(String(255), nullable=True)
    CreatedAt = Column(DateTime, default=lambda: datetime.now(timezone.utc))

    Messages = relationship("AiChatMessage", back_populates="Session", cascade="all, delete-orphan")
