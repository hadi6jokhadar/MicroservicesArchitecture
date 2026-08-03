import uuid
from datetime import datetime, timezone
from sqlalchemy import String, Integer, DateTime, Float
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column
from core.database import Base


class AiTokenUsageLog(Base):
    __tablename__ = "AiTokenUsageLog"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    UserId: Mapped[int | None] = mapped_column(Integer, nullable=True, index=True)
    ModelName: Mapped[str] = mapped_column(String(100), nullable=False)
    PromptTokens: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    CompletionTokens: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    TotalTokens: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    Endpoint: Mapped[str] = mapped_column(String(255), nullable=False)
    # Audio-billed calls (e.g. /api/v1/transcription) have no meaningful token count —
    # PromptTokens/CompletionTokens/TotalTokens stay 0 for these rows, and the actual
    # billed quantity (seconds of audio) is recorded here instead.
    AudioDurationSeconds: Mapped[float | None] = mapped_column(Float, nullable=True)
    CreatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))
