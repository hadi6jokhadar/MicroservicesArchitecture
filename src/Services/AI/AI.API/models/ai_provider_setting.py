import uuid
from sqlalchemy import String, Enum
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column
from core.database import Base
from models.enums import ModelTypeEnum


class AiProviderSettings(Base):
    __tablename__ = "AiProviderSettings"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    ModelType: Mapped[ModelTypeEnum] = mapped_column(Enum(ModelTypeEnum), nullable=False, default=ModelTypeEnum.Text)
    Provider: Mapped[str] = mapped_column(String(50), nullable=False)
    ApiKey: Mapped[str] = mapped_column(String(500), nullable=False)
    ModelName: Mapped[str] = mapped_column(String(100), nullable=False)
