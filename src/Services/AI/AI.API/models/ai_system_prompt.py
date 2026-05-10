import uuid
from datetime import datetime
from sqlalchemy import String, Text, DateTime
from sqlalchemy.sql import func
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column
from core.database import Base


class AiSystemPrompt(Base):
    __tablename__ = "AiSystemPrompt"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    Name: Mapped[str] = mapped_column(String(100), nullable=False, index=True)
    PromptText: Mapped[str] = mapped_column(Text, nullable=False)
    ResponseFormat: Mapped[str | None] = mapped_column(Text, nullable=True)
    CreatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=func.now())
