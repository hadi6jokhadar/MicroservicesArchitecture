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
    # Caller-supplied correlation id (e.g. "nasheed:job:123") so multiple otherwise-unrelated calls
    # belonging to one batch/pipeline run can be found together later — stored on the newly-created
    # AiChatSession and on the AiTokenUsageLog row, never used to alter prompt content. Ignored when
    # session_id is also provided (that session already has whatever PipelineRunId it was created with).
    pipeline_run_id: Optional[str] = None

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
    # See ChatRequest.pipeline_run_id — same correlation id, camelCase here to match this
    # endpoint's existing naming convention.
    pipelineRunId: Optional[str] = None


class EmbeddingResponse(BaseModel):
    embedding: List[float]
    model: str


# ---------------------------------------------------------------------------
# Transcription (ASR with timestamps)
# ---------------------------------------------------------------------------

class TranscriptionRequest(BaseModel):
    settings_key: str = Field(min_length=1)
    file_ids: List[int] = Field(min_length=1)
    language: Optional[str] = None
    # See ChatRequest.pipeline_run_id — same correlation id, recorded on the AiTokenUsageLog row
    # (transcription never creates an AiChatSession).
    pipeline_run_id: Optional[str] = None


class TranscriptionSegment(BaseModel):
    start: float
    end: float
    text: str
    # Whisper's own confidence signals for this segment — None if the provider doesn't report them.
    # no_speech_prob close to 1.0 means Whisper itself believes this segment is silence/non-speech
    # that got transcribed anyway (its most direct hallucination signal); avg_logprob close to 0 is
    # confident, very negative (e.g. below -1) means low confidence in the words chosen.
    avg_logprob: Optional[float] = None
    no_speech_prob: Optional[float] = None


class TranscriptionWord(BaseModel):
    start: float
    end: float
    word: str


class TranscriptionResponse(BaseModel):
    text: str
    language: Optional[str] = None
    duration: Optional[float] = None
    segments: List[TranscriptionSegment] = Field(default_factory=list)
    words: List[TranscriptionWord] = Field(default_factory=list)
    # Echoes AiProviderSettings.NoSpeechProbThreshold for the settings_key used — lets the calling
    # service apply the admin-configured hallucination-filter threshold instead of a hardcoded one.
    # None means the admin hasn't set one; the caller falls back to its own default in that case.
    no_speech_prob_threshold: Optional[float] = None
