import uuid
from typing import TYPE_CHECKING
from sqlalchemy import ForeignKey
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship
from core.database import Base

if TYPE_CHECKING:
    from .ai_chat_message import AiChatMessage


class AiChatMessageFile(Base):
    __tablename__ = "AiChatMessageFile"

    MessageId: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("AiChatMessage.Id", ondelete="CASCADE"),
        primary_key=True,
    )
    FileId: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)

    Message: Mapped["AiChatMessage"] = relationship("AiChatMessage", back_populates="Files")
