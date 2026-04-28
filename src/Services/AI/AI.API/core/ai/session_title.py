"""Session title auto-generation.

After the first AI response in a session that has no title, a lightweight
background task is enqueued to call the AI with the "Session-Title" system
prompt and persist the resulting title on AiChatSession.
"""
import logging
import uuid
from typing import Optional

from fastapi import BackgroundTasks
from litellm import acompletion  # type: ignore
from sqlalchemy.future import select

from core.ai.db_queries import get_settings_by_key, get_system_prompt_by_key
from core.ai.utils import build_litellm_model
from core.database import AsyncSessionFactory
from models import AiChatSession, AiTokenUsageLog

logger = logging.getLogger(__name__)

SESSION_TITLE_PROMPT_NAME = "Session-Title"
SESSION_TITLE_SETTINGS_KEY = "QwenAI-qwen3-vl-32b-instruct"
# Cap title at 255 chars to match the column length.
_MAX_TITLE_LENGTH = 255


async def generate_session_title_background(
    session_id: uuid.UUID,
    user_content: str,
    tenant_id: Optional[str],
) -> None:
    """Background task: generate and persist a title for a session that has none.

    Exits silently when:
    - The session already has a title (concurrent write guard).
    - The "Session-Title" system prompt does not exist.
    - The AI provider settings are not found.
    - The AI call fails for any reason (non-critical path).
    """
    async with AsyncSessionFactory() as db:
        try:
            # 1. Fetch the session and bail out if it already has a title.
            result = await db.execute(
                select(AiChatSession).where(AiChatSession.Id == session_id)
            )
            session = result.scalar_one_or_none()
            if session is None or session.Title:
                return

            # 2. Fetch the "Session-Title" system prompt.
            prompt_obj = await get_system_prompt_by_key(
                SESSION_TITLE_PROMPT_NAME, tenant_id, db
            )
            if prompt_obj is None:
                logger.debug(
                    "Session-Title system prompt not found; skipping title generation "
                    "for session %s", session_id
                )
                return

            # 3. Fetch AI provider settings using the dedicated title-generation key.
            try:
                ai_settings = await get_settings_by_key(SESSION_TITLE_SETTINGS_KEY, tenant_id, db)
            except Exception:
                logger.warning(
                    "AI settings '%s' not found; skipping title generation for session %s",
                    SESSION_TITLE_SETTINGS_KEY,
                    session_id,
                )
                return

            # 4. Build the messages and call the AI.
            litellm_model = build_litellm_model(ai_settings.Provider, ai_settings.ModelName)
            completion_kwargs = {
                "model": litellm_model,
                "messages": [
                    {"role": "system", "content": prompt_obj.PromptText},
                    {"role": "user", "content": user_content},
                ],
                "api_key": str(ai_settings.ApiKey),
                "stream": False,
            }
            if ai_settings.ApiBaseUrl:
                completion_kwargs["api_base"] = ai_settings.ApiBaseUrl
            if ai_settings.MaxCompletionTokens is not None:
                completion_kwargs["max_tokens"] = ai_settings.MaxCompletionTokens
            if ai_settings.Temperature is not None:
                completion_kwargs["temperature"] = ai_settings.Temperature

            response = await acompletion(**completion_kwargs)

            # 5. Extract token usage from the response.
            usage = getattr(response, "usage", None)
            prompt_tokens: int = (getattr(usage, "prompt_tokens", 0) or 0) if usage else 0
            completion_tokens: int = (getattr(usage, "completion_tokens", 0) or 0) if usage else 0

            # 6. Extract and clean up the generated title.
            title: Optional[str] = None
            if getattr(response, "choices", None):
                message = getattr(response.choices[0], "message", None)
                if message is not None:
                    raw = getattr(message, "content", None) or ""
                    title = raw.strip().strip('"').strip("'").strip()

            if not title:
                logger.debug(
                    "AI returned empty title for session %s; skipping update", session_id
                )
                return

            if len(title) > _MAX_TITLE_LENGTH:
                title = title[:_MAX_TITLE_LENGTH]

            # 7. Persist the title (re-check to avoid overwriting a concurrent update).
            if session.Title:
                return

            session.Title = title
            db.add(AiTokenUsageLog(
                TenantId=tenant_id,
                UserId=session.UserId,
                ModelName=str(ai_settings.ModelName),
                Endpoint="/api/v1/chat/session-title",
                PromptTokens=prompt_tokens,
                CompletionTokens=completion_tokens,
                TotalTokens=prompt_tokens + completion_tokens,
            ))
            await db.commit()
            logger.debug("Session %s title set to: %s", session_id, title)

        except Exception as exc:
            logger.error(
                "Unexpected error generating session title for %s: %s",
                session_id,
                exc,
                exc_info=True,
            )


def schedule_session_title_task(
    background_tasks: BackgroundTasks,
    session_id: uuid.UUID,
    user_content: str,
    tenant_id: Optional[str],
) -> None:
    """Enqueue the session title generation as a background task."""
    background_tasks.add_task(
        generate_session_title_background,
        session_id,
        user_content,
        tenant_id,
    )
