import uuid
from sqlalchemy import String, Text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column
from core.database import Base


class AiSystemPrompt(Base):
    __tablename__ = "AiSystemPrompt"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    Name: Mapped[str] = mapped_column(String(100), nullable=False, index=True)
    PromptText: Mapped[str] = mapped_column(Text, nullable=False)
