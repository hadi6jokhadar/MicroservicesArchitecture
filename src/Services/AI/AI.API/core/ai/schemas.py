from pydantic import BaseModel, Field, UUID4, model_validator
from typing import List, Optional, Literal


# ---------------------------------------------------------------------------
# Chat
# ---------------------------------------------------------------------------

class ChatMessage(BaseModel):
    role: Literal["system", "user", "assistant", "tool"]
    content: str = Field(min_length=1)


class ChatRequest(BaseModel):
    session_id: Optional[UUID4] = None
    settings_key: str = Field(min_length=1)
    system_prompt_key: Optional[str] = None
    messages: Optional[List[ChatMessage]] = None
    file_ids: List[int] = Field(default_factory=list)
    max_completion_tokens: Optional[int] = Field(default=None, ge=1, le=32768)
    generate_session_title: bool = False

    @model_validator(mode="after")
    def validate_message_or_prompt(self) -> "ChatRequest":
        has_messages = bool(self.messages)
        has_system_prompt_key = bool(self.system_prompt_key and self.system_prompt_key.strip())
        if not has_messages and not has_system_prompt_key:
            raise ValueError("Either messages or system_prompt_key must be provided.")
        return self


class ChatSingleResponse(BaseModel):
    session_id: UUID4
    content: str
    prompt_tokens: int
    completion_tokens: int
    total_tokens: int


# ---------------------------------------------------------------------------
# Embedding
# ---------------------------------------------------------------------------

class EmbeddingRequest(BaseModel):
    settingsKey: str = Field(min_length=1)
    text: str = Field(min_length=1)


class EmbeddingResponse(BaseModel):
    embedding: List[float]
    model: str
