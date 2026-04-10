import uuid
from datetime import datetime, timezone
from typing import TYPE_CHECKING
from sqlalchemy import String, DateTime
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship
from core.database import Base

if TYPE_CHECKING:
    from .ai_chat_message import AiChatMessage


class AiChatSession(Base):
    __tablename__ = "AiChatSession"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str] = mapped_column(String(100), nullable=False, index=True)
    UserId: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), nullable=False, index=True)
    Title: Mapped[str | None] = mapped_column(String(255), nullable=True)
    CreatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))

    Messages: Mapped[list["AiChatMessage"]] = relationship(
        "AiChatMessage",
        back_populates="Session",
        cascade="all, delete-orphan",
    )
