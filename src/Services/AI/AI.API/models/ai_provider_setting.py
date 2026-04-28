import uuid
from sqlalchemy import String, Enum, UniqueConstraint, Float, Integer, Boolean
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column
from core.database import Base
from models.enums import ModelTypeEnum


class AiProviderSettings(Base):
    __tablename__ = "AiProviderSettings"
    __table_args__ = (UniqueConstraint("Key", name="uq_ai_provider_settings_key"),)

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    Key: Mapped[str] = mapped_column(String(100), nullable=False, unique=True, index=True)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    ModelType: Mapped[ModelTypeEnum] = mapped_column(Enum(ModelTypeEnum), nullable=False, default=ModelTypeEnum.Text)
    Provider: Mapped[str] = mapped_column(String(50), nullable=False)
    ApiKey: Mapped[str] = mapped_column(String(500), nullable=False)
    ModelName: Mapped[str] = mapped_column(String(100), nullable=False)
    # Provider-specific API base URL (e.g. Qwen OpenAI-compatible endpoint)
    ApiBaseUrl: Mapped[str | None] = mapped_column(String(500), nullable=True)
    # Sampling temperature (0.0–2.0). None means use provider default.
    Temperature: Mapped[float | None] = mapped_column(Float, nullable=True)
    # Whether to stream responses by default. None means caller decides.
    Stream: Mapped[bool | None] = mapped_column(Boolean, nullable=True)
    # Maximum tokens for the model completion. None means provider default.
    MaxCompletionTokens: Mapped[int | None] = mapped_column(Integer, nullable=True)
    # Top-p nucleus sampling. None means provider default.
    TopP: Mapped[float | None] = mapped_column(Float, nullable=True)
    # Frequency penalty (-2.0–2.0). None means provider default.
    FrequencyPenalty: Mapped[float | None] = mapped_column(Float, nullable=True)
    # Presence penalty (-2.0–2.0). None means provider default.
    PresencePenalty: Mapped[float | None] = mapped_column(Float, nullable=True)
    # Description of the setting
    Description: Mapped[str | None] = mapped_column(String(500), nullable=True)
