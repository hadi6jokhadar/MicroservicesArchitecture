import uuid
from datetime import datetime, timezone
from typing import TYPE_CHECKING
from sqlalchemy import String, Integer, DateTime, ForeignKey, Text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship
from core.database import Base

if TYPE_CHECKING:
    from .ai_chat_message_file import AiChatMessageFile
    from .ai_chat_session import AiChatSession


class AiChatMessage(Base):
    __tablename__ = "AiChatMessage"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    SessionId: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("AiChatSession.Id", ondelete="CASCADE"),
        nullable=False,
    )
    Role: Mapped[str] = mapped_column(String(50), nullable=False)
    Content: Mapped[str] = mapped_column(Text, nullable=False)
    PromptTokens: Mapped[int] = mapped_column(Integer, default=0, nullable=False)
    CompletionTokens: Mapped[int] = mapped_column(Integer, default=0, nullable=False)
    CreatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))

    Session: Mapped["AiChatSession"] = relationship("AiChatSession", back_populates="Messages")
    Files: Mapped[list["AiChatMessageFile"]] = relationship(
        "AiChatMessageFile",
        back_populates="Message",
        cascade="all, delete-orphan",
    )
