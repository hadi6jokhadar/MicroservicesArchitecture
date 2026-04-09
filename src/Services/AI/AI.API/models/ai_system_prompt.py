import uuid
from sqlalchemy import Column, String, Text
from sqlalchemy.dialects.postgresql import UUID
from core.database import Base

class AiSystemPrompt(Base):
    __tablename__ = "AiSystemPrompt"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=True, index=True)
    Name = Column(String(100), nullable=False, index=True) # e.g. "EnhanceText"
    PromptText = Column(Text, nullable=False)
