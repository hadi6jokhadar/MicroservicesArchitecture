import uuid
from datetime import datetime
from sqlalchemy import Column, String, Integer, DateTime, ForeignKey, Enum, Text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship
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

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=True, index=True) # Null = Global Settings
    ModelType = Column(Enum(ModelTypeEnum), nullable=False, default=ModelTypeEnum.Text)
    Provider = Column(String(50), nullable=False) # e.g. "OpenAI", "Azure"
    ApiKey = Column(String(500), nullable=False)
    ModelName = Column(String(100), nullable=False) # e.g. "gpt-4o"

class AiSystemPrompt(Base):
    __tablename__ = "AiSystemPrompt"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=True, index=True)
    Name = Column(String(100), nullable=False, index=True) # e.g. "EnhanceText"
    PromptText = Column(Text, nullable=False)

class AiChatSession(Base):
    __tablename__ = "AiChatSession"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=False, index=True)
    UserId = Column(UUID(as_uuid=True), nullable=False, index=True)
    Title = Column(String(255), nullable=True)
    CreatedAt = Column(DateTime, default=datetime.utcnow)

    Messages = relationship("AiChatMessage", back_populates="Session", cascade="all, delete-orphan")

class AiChatMessage(Base):
    __tablename__ = "AiChatMessage"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    SessionId = Column(UUID(as_uuid=True), ForeignKey("AiChatSession.Id", ondelete="CASCADE"), nullable=False)
    Role = Column(String(50), nullable=False) # "System", "User", "Assistant"
    Content = Column(Text, nullable=False)
    PromptTokens = Column(Integer, default=0)
    CompletionTokens = Column(Integer, default=0)
    CreatedAt = Column(DateTime, default=datetime.utcnow)

    Session = relationship("AiChatSession", back_populates="Messages")
    Files = relationship("AiChatMessageFile", back_populates="Message", cascade="all, delete-orphan")

class AiChatMessageFile(Base):
    __tablename__ = "AiChatMessageFile"

    MessageId = Column(UUID(as_uuid=True), ForeignKey("AiChatMessage.Id", ondelete="CASCADE"), primary_key=True)
    FileId = Column(UUID(as_uuid=True), primary_key=True)

    Message = relationship("AiChatMessage", back_populates="Files")

class AiTokenUsageLog(Base):
    __tablename__ = "AiTokenUsageLog"

    Id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    TenantId = Column(String(100), nullable=True, index=True)
    UserId = Column(UUID(as_uuid=True), nullable=True, index=True)
    ModelName = Column(String(100), nullable=False)
    PromptTokens = Column(Integer, nullable=False, default=0)
    CompletionTokens = Column(Integer, nullable=False, default=0)
    TotalTokens = Column(Integer, nullable=False, default=0)
    Endpoint = Column(String(255), nullable=False) # e.g. "/api/v1/chat/stream"
    CreatedAt = Column(DateTime, default=datetime.utcnow)
