import uuid
from datetime import datetime, timezone
from sqlalchemy import Column, String, Integer, DateTime, ForeignKey, Text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship
from core.database import Base

class AiChatMessage(Base):
    __tablename__ = "AiChatMessage"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    SessionId = Column(UUID(as_uuid=True), ForeignKey("AiChatSession.Id", ondelete="CASCADE"), nullable=False)
    Role = Column(String(50), nullable=False) # "System", "User", "Assistant"
    Content = Column(Text, nullable=False)
    PromptTokens = Column(Integer, default=0)
    CompletionTokens = Column(Integer, default=0)
    CreatedAt = Column(DateTime, default=lambda: datetime.now(timezone.utc))

    Session = relationship("AiChatSession", back_populates="Messages")
    Files = relationship("AiChatMessageFile", back_populates="Message", cascade="all, delete-orphan")
