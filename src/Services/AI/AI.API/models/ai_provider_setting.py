import uuid
from sqlalchemy import Column, String, Enum
from sqlalchemy.dialects.postgresql import UUID
from core.database import Base
from models.enums import ModelTypeEnum

class AiProviderSettings(Base):
    __tablename__ = "AiProviderSettings"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=True, index=True) # Null = Global Settings
    ModelType = Column(Enum(ModelTypeEnum), nullable=False, default=ModelTypeEnum.Text)
    Provider = Column(String(50), nullable=False) # e.g. "OpenAI", "Azure"
    ApiKey = Column(String(500), nullable=False)
    ModelName = Column(String(100), nullable=False) # e.g. "gpt-4o"
