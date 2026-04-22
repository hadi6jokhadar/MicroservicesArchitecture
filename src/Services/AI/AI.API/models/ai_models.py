import uuid
from datetime import datetime, timezone
from sqlalchemy import String, Integer, DateTime, ForeignKey, Enum, Text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column, relationship
import enum
from core.database import Base

class ModelTypeEnum(str, enum.Enum):
    Text = "Text"
    Vision = "Vision"
    Audio = "Audio"
    Embedding = "Embedding"
    ImageGeneration = "ImageGeneration"

class AiProviderSettings(Base):
    __tablename__ = "AiProviderSettings"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    Key: Mapped[str] = mapped_column(String(100), nullable=False, unique=True, index=True)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    ModelType: Mapped[ModelTypeEnum] = mapped_column(Enum(ModelTypeEnum), nullable=False, default=ModelTypeEnum.Text)
    Provider: Mapped[str] = mapped_column(String(50), nullable=False)
    ApiKey: Mapped[str] = mapped_column(String(500), nullable=False)
    ModelName: Mapped[str] = mapped_column(String(100), nullable=False)

class AiSystemPrompt(Base):
    __tablename__ = "AiSystemPrompt"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    Name: Mapped[str] = mapped_column(String(100), nullable=False, index=True)
    PromptText: Mapped[str] = mapped_column(Text, nullable=False)

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

class AiChatMessageFile(Base):
    __tablename__ = "AiChatMessageFile"

    MessageId: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("AiChatMessage.Id", ondelete="CASCADE"),
        primary_key=True,
    )
    FileId: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)

    Message: Mapped["AiChatMessage"] = relationship("AiChatMessage", back_populates="Files")

class AiTokenUsageLog(Base):
    __tablename__ = "AiTokenUsageLog"

    Id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId: Mapped[str | None] = mapped_column(String(100), nullable=True, index=True)
    UserId: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True, index=True)
    ModelName: Mapped[str] = mapped_column(String(100), nullable=False)
    PromptTokens: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    CompletionTokens: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    TotalTokens: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    Endpoint: Mapped[str] = mapped_column(String(255), nullable=False)
    CreatedAt: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))
