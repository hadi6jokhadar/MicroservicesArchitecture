import uuid
from datetime import datetime, timezone
from typing import TYPE_CHECKING
from sqlalchemy import String, DateTime, Integer
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship
from core.database import Base

if TYPE_CHECKING:
    from .ai_chat_message import AiChatMessage


class AiChatSession(Base):
    __tablename__ = "AiChatSession"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str] = mapped_column(String(100), nullable=False, index=True)
    UserId: Mapped[int] = mapped_column(Integer, nullable=False, index=True)
    Title: Mapped[str | None] = mapped_column(String(255), nullable=True)
    # Caller-supplied correlation id (e.g. "nasheed:job:123") grouping this session with other
    # sessions/log rows from the same batch/pipeline run — set once at creation, never read back
    # into prompt content. See ChatRequest.pipeline_run_id.
    PipelineRunId: Mapped[str | None] = mapped_column(String(200), nullable=True, index=True)
    CreatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))

    Messages: Mapped[list["AiChatMessage"]] = relationship(
        "AiChatMessage",
        back_populates="Session",
        cascade="all, delete-orphan",
    )
