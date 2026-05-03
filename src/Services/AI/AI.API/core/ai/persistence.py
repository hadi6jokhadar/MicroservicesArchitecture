import json
import uuid
from typing import Any, List, Optional

from fastapi import BackgroundTasks

from core.database import AsyncSessionFactory
from models import AiChatMessage, AiTokenUsageLog


# ---------------------------------------------------------------------------
# Background tasks
# ---------------------------------------------------------------------------

async def log_token_usage_background(
    tenant_id: Optional[str],
    user_id: Optional[int],
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int,
) -> None:
    """Opens its own DB session so it can run safely after the request completes."""
    async with AsyncSessionFactory() as db:
        usage_log = AiTokenUsageLog(
            TenantId=tenant_id,
            UserId=user_id,
            ModelName=model_name,
            Endpoint=endpoint,
            PromptTokens=prompt_tokens,
            CompletionTokens=completion_tokens,
            TotalTokens=prompt_tokens + completion_tokens,
        )
        db.add(usage_log)
        await db.commit()


async def persist_messages_background(
    session_id: uuid.UUID,
    model_input_messages: List[dict[str, Any]],
    assistant_content: Any,
    prompt_tokens: int,
    completion_tokens: int,
) -> None:
    """Persist exact model-input messages plus assistant output in its own DB session."""

    def _serialize_content(content: Any) -> str:
        if isinstance(content, str):
            return content
        try:
            return json.dumps(content, ensure_ascii=False)
        except (TypeError, ValueError):
            return str(content)

    async with AsyncSessionFactory() as db:
        for index, message in enumerate(model_input_messages):
            role = str(message.get("role") or "user")
            content = _serialize_content(message.get("content"))
            db.add(
                AiChatMessage(
                    SessionId=session_id,
                    Role=role,
                    Content=content,
                    PromptTokens=prompt_tokens if index == len(model_input_messages) - 1 else 0,
                    CompletionTokens=0,
                )
            )

        db.add(AiChatMessage(
            SessionId=session_id,
            Role="assistant",
            Content=_serialize_content(assistant_content),
            PromptTokens=0,
            CompletionTokens=completion_tokens,
        ))
        await db.commit()


# ---------------------------------------------------------------------------
# Scheduler helpers (enqueue the background tasks)
# ---------------------------------------------------------------------------

def schedule_chat_persistence_tasks(
    background_tasks: BackgroundTasks,
    session_id: uuid.UUID,
    model_input_messages: List[dict[str, Any]],
    complete_response: str,
    tenant_id: Optional[str],
    user_id: Optional[int],
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int,
) -> None:
    """Enqueue message persistence + token usage logging for a chat turn."""
    background_tasks.add_task(
        persist_messages_background,
        session_id,
        model_input_messages,
        complete_response,
        prompt_tokens,
        completion_tokens,
    )
    background_tasks.add_task(
        log_token_usage_background,
        tenant_id,
        user_id,
        model_name,
        endpoint,
        prompt_tokens,
        completion_tokens,
    )


def schedule_token_log_task(
    background_tasks: BackgroundTasks,
    tenant_id: Optional[str],
    user_id: Optional[int],
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int,
) -> None:
    """Enqueue token usage logging only (used for ASR, which has no chat session)."""
    background_tasks.add_task(
        log_token_usage_background,
        tenant_id,
        user_id,
        model_name,
        endpoint,
        prompt_tokens,
        completion_tokens,
    )
