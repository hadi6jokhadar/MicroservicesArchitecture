import uuid
from sqlalchemy import Column, ForeignKey
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship
from core.database import Base

class AiChatMessageFile(Base):
    __tablename__ = "AiChatMessageFile"

    MessageId = Column(UUID(as_uuid=True), ForeignKey("AiChatMessage.Id", ondelete="CASCADE"), primary_key=True)
    FileId = Column(UUID(as_uuid=True), primary_key=True)

    Message = relationship("AiChatMessage", back_populates="Files")
