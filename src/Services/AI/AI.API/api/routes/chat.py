from typing import Any, AsyncGenerator, List, Optional
import json
import logging

from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException, status
from fastapi.responses import StreamingResponse
from litellm import acompletion  # type: ignore
from sqlalchemy.ext.asyncio import AsyncSession

from api.attributes import optional_tenant
from api.dependencies import get_tenant_id, require_auth
from core.ai.chat_workflow import build_chat_runtime_context
from core.ai.persistence import (
    log_token_usage_background,  # noqa: F401
    persist_messages_background,  # noqa: F401
)
from core.ai.schemas import ChatRequest, ChatSingleResponse
from core.ai.session_title import schedule_session_title_task
from core.ai.utils import estimate_tokens_if_missing, map_litellm_exception_to_http
from core.database import get_db

logger = logging.getLogger(__name__)

router = APIRouter()


def _schedule_chat_persistence_tasks(
    background_tasks: BackgroundTasks,
    session_id: Any,
    model_input_messages: List[dict[str, Any]],
    assistant_content: Any,
    tenant_id: Optional[str],
    user_id: Optional[int],
    model_name: str,
    endpoint: str,
    prompt_tokens: int,
    completion_tokens: int,
) -> None:
    """Route-local scheduler to keep test patching stable in api.routes.chat."""
    background_tasks.add_task(
        persist_messages_background,
        session_id,
        model_input_messages,
        assistant_content,
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


def _has_audio_url_blocks(messages: List[dict[str, Any]]) -> bool:
    """Return True if any message contains an input_audio content block.

    Qwen omni models (Dashscope compatible-mode) require the extra
    ``modalities: [\"text\"]`` field in the request body when audio
    blocks are present, otherwise Dashscope returns HTTP 400.
    """
    return any(
        isinstance(msg.get("content"), list)
        and any(block.get("type") == "input_audio" for block in msg["content"])
        for msg in messages
    )


@router.post("/stream")
@optional_tenant
async def chat_stream(
    request: ChatRequest,
    background_tasks: BackgroundTasks,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth),
):
    ctx = await build_chat_runtime_context(request, tenant_id, db, auth)

    async def generate() -> AsyncGenerator[str, None]:
        complete_response = ""
        prompt_tokens = 0
        completion_tokens = 0
        finish_reason: Optional[str] = None
        success = False

        try:
            completion_kwargs: dict[str, Any] = {
                "model": ctx["litellm_model"],
                "messages": ctx["litellm_messages"],
                "api_key": ctx["provider_api_key"],
                "stream": True,
                "stream_options": {"include_usage": True},
            }
            if ctx["provider_api_base"]:
                completion_kwargs["api_base"] = ctx["provider_api_base"]
            if ctx["response_format"]:
                completion_kwargs["response_format"] = ctx["response_format"]
            if ctx["max_completion_tokens"] is not None:
                completion_kwargs["max_tokens"] = ctx["max_completion_tokens"]
            if ctx["temperature"] is not None:
                completion_kwargs["temperature"] = ctx["temperature"]
            if ctx["top_p"] is not None:
                completion_kwargs["top_p"] = ctx["top_p"]
            if ctx["frequency_penalty"] is not None:
                completion_kwargs["frequency_penalty"] = ctx["frequency_penalty"]
            if ctx["presence_penalty"] is not None:
                completion_kwargs["presence_penalty"] = ctx["presence_penalty"]
            # Qwen omni (Dashscope) requires modalities when audio_url blocks are present.
            if _has_audio_url_blocks(ctx["litellm_messages"]):
                completion_kwargs["extra_body"] = {"modalities": ["text"]}

            response = await acompletion(**completion_kwargs)  # type: ignore[assignment]

            async for chunk in response:  # type: ignore[union-attr]
                if hasattr(chunk, "usage") and chunk.usage:
                    prompt_tokens = chunk.usage.prompt_tokens or 0
                    completion_tokens = chunk.usage.completion_tokens or 0

                if not chunk.choices:
                    continue

                first_choice = chunk.choices[0]
                chunk_finish_reason = getattr(first_choice, "finish_reason", None)
                if isinstance(chunk_finish_reason, str) and chunk_finish_reason:
                    finish_reason = chunk_finish_reason

                delta = getattr(first_choice, "delta", None)
                delta_content = getattr(delta, "content", None)
                if delta_content is None:
                    continue

                streamed = delta_content if isinstance(delta_content, str) else json.dumps(delta_content)
                if streamed:
                    complete_response += streamed
                    yield f"data: {json.dumps({'session_id': str(ctx['session_id']), 'content': streamed})}\n\n"

            success = True
            yield (
                "data: "
                + json.dumps({
                    "session_id": str(ctx["session_id"]),
                    "done": True,
                    "finish_reason": finish_reason,
                    "is_truncated": finish_reason in {"length", "max_tokens"},
                })
                + "\n\n"
            )
            yield "data: [DONE]\n\n"

        except Exception as e:
            logger.error("Provider error during stream: %s", e, exc_info=True)
            http_exc = map_litellm_exception_to_http(e)
            yield f"data: {json.dumps({'error': str(e), 'status_code': http_exc.status_code})}\n\n"

        finally:
            if success:
                prompt_tokens, completion_tokens = estimate_tokens_if_missing(
                    prompt_tokens, completion_tokens, ctx["litellm_messages"], complete_response
                )
                _schedule_chat_persistence_tasks(
                    background_tasks,
                    ctx["session_id"],
                    ctx["litellm_messages"],
                    complete_response,
                    tenant_id,
                    ctx["user_id"],
                    ctx["model_name"],
                    "/api/v1/chat/stream",
                    prompt_tokens,
                    completion_tokens,
                )
                if request.generate_session_title:
                    schedule_session_title_task(
                        background_tasks,
                        ctx["session_id"],
                        ctx["user_content"],
                        tenant_id,
                    )

    return StreamingResponse(generate(), media_type="text/event-stream")


@router.post("/single", response_model=ChatSingleResponse)
@optional_tenant
async def chat_single_response(
    request: ChatRequest,
    background_tasks: BackgroundTasks,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth),
):
    ctx = await build_chat_runtime_context(request, tenant_id, db, auth)

    try:
        completion_kwargs: dict[str, Any] = {
            "model": ctx["litellm_model"],
            "messages": ctx["litellm_messages"],
            "api_key": ctx["provider_api_key"],
            "stream": False,
        }
        if ctx["provider_api_base"]:
            completion_kwargs["api_base"] = ctx["provider_api_base"]
        if ctx["response_format"]:
            completion_kwargs["response_format"] = ctx["response_format"]
        if ctx["max_completion_tokens"] is not None:
            completion_kwargs["max_tokens"] = ctx["max_completion_tokens"]
        if ctx["temperature"] is not None:
            completion_kwargs["temperature"] = ctx["temperature"]
        if ctx["top_p"] is not None:
            completion_kwargs["top_p"] = ctx["top_p"]
        if ctx["frequency_penalty"] is not None:
            completion_kwargs["frequency_penalty"] = ctx["frequency_penalty"]
        if ctx["presence_penalty"] is not None:
            completion_kwargs["presence_penalty"] = ctx["presence_penalty"]
        # Qwen omni (Dashscope) requires modalities when audio_url blocks are present.
        if _has_audio_url_blocks(ctx["litellm_messages"]):
            completion_kwargs["extra_body"] = {"modalities": ["text"]}

        response = await acompletion(**completion_kwargs)  # type: ignore[assignment]
    except Exception as e:
        logger.error("Provider error during single response: %s", e, exc_info=True)
        raise map_litellm_exception_to_http(e)

    assistant_content: Any = ""
    if getattr(response, "choices", None):
        first_choice = response.choices[0]  # type: ignore[union-attr]
        message = getattr(first_choice, "message", None)
        if message is not None:
            assistant_content = getattr(message, "content", "") or ""

    assistant_content_text = (
        assistant_content
        if isinstance(assistant_content, str)
        else json.dumps(assistant_content)
    )

    usage = getattr(response, "usage", None)
    prompt_tokens = (getattr(usage, "prompt_tokens", 0) or 0) if usage else 0
    completion_tokens = (getattr(usage, "completion_tokens", 0) or 0) if usage else 0
    prompt_tokens, completion_tokens = estimate_tokens_if_missing(
        prompt_tokens, completion_tokens, ctx["litellm_messages"], assistant_content_text
    )

    _schedule_chat_persistence_tasks(
        background_tasks,
        ctx["session_id"],
        ctx["litellm_messages"],
        assistant_content,
        tenant_id,
        ctx["user_id"],
        ctx["model_name"],
        "/api/v1/chat/single",
        prompt_tokens,
        completion_tokens,
    )
    if request.generate_session_title:
        schedule_session_title_task(
            background_tasks,
            ctx["session_id"],
            ctx["user_content"],
            tenant_id,
        )

    return ChatSingleResponse(
        session_id=ctx["session_id"],
        content=assistant_content_text,
        prompt_tokens=prompt_tokens,
        completion_tokens=completion_tokens,
        total_tokens=prompt_tokens + completion_tokens,
    )
