import logging
from typing import Optional

from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException, status
from litellm import atranscription  # type: ignore
from sqlalchemy.ext.asyncio import AsyncSession

from api.attributes import optional_tenant
from api.dependencies import get_tenant_id, require_auth
from core.ai.db_queries import get_settings_by_key
from core.ai.file_context import file_manager_client
from core.ai.multimodal_utils import (
    fetch_file_bytes_with_fallback,
    resolve_internal_url,
    resolve_mime_type,
)
from core.ai.persistence import schedule_token_log_task
from core.ai.schemas import (
    TranscriptionRequest,
    TranscriptionResponse,
    TranscriptionSegment,
    TranscriptionWord,
)
from core.ai.utils import build_litellm_model, extract_user_id, map_litellm_exception_to_http
from core.database import get_db
from models import ModelTypeEnum

logger = logging.getLogger(__name__)

router = APIRouter()


@router.post("", response_model=TranscriptionResponse)
@optional_tenant
async def transcribe_audio(
    request: TranscriptionRequest,
    background_tasks: BackgroundTasks,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth),
):
    """Transcribe a single audio file with segment- and word-level timestamps via a dedicated ASR model.

    Unlike /chat/single, this calls a real speech-to-text model (e.g. Whisper) instead of a
    generative chat completion, so timestamps come from audio alignment rather than being
    estimated by an LLM. Only the first entry in file_ids is used.
    """
    ai_settings = await get_settings_by_key(request.settings_key, tenant_id, db)

    if ai_settings.ModelType != ModelTypeEnum.Audio:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=(
                f"Settings key '{request.settings_key}' is not an Audio model "
                f"(ModelType={ai_settings.ModelType.value})."
            ),
        )

    files_metadata = await file_manager_client.get_files_by_ids(request.file_ids, tenant_id)
    if not files_metadata:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="No files found for the provided file_ids.",
        )

    file_meta = files_metadata[0]
    external_url = file_meta.get("external_url") or file_meta.get("externalUrl")
    internal_url = resolve_internal_url(file_meta)
    primary_url = external_url or internal_url
    if not primary_url:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Attached file has no resolvable URL.",
        )

    mime_type = resolve_mime_type(file_meta)
    audio_bytes = await fetch_file_bytes_with_fallback(primary_url, internal_url)
    filename = f"{file_meta.get('name', 'audio')}{file_meta.get('extension', '')}"

    litellm_model = build_litellm_model(ai_settings.Provider, ai_settings.ModelName)

    transcription_kwargs: dict = {
        "model": litellm_model,
        "file": (filename, audio_bytes, mime_type),
        "api_key": ai_settings.ApiKey,
        "response_format": "verbose_json",
        "timestamp_granularities": ["segment", "word"],
    }
    if ai_settings.ApiBaseUrl:
        transcription_kwargs["api_base"] = ai_settings.ApiBaseUrl
    if request.language:
        transcription_kwargs["language"] = request.language

    try:
        response = await atranscription(**transcription_kwargs)  # type: ignore[assignment]
    except Exception as e:
        logger.error("Provider error during transcription: %s", e, exc_info=True)
        raise map_litellm_exception_to_http(e)

    def _get(seg, key: str):
        return seg.get(key) if isinstance(seg, dict) else getattr(seg, key, None)

    raw_segments = getattr(response, "segments", None) or []
    segments = [
        TranscriptionSegment(
            start=float(_get(seg, "start")),
            end=float(_get(seg, "end")),
            text=str(_get(seg, "text")).strip(),
            avg_logprob=(
                float(_get(seg, "avg_logprob")) if _get(seg, "avg_logprob") is not None else None
            ),
            no_speech_prob=(
                float(_get(seg, "no_speech_prob")) if _get(seg, "no_speech_prob") is not None else None
            ),
        )
        for seg in raw_segments
    ]

    raw_words = getattr(response, "words", None) or []
    words = [
        TranscriptionWord(
            start=float(w["start"] if isinstance(w, dict) else w.start),
            end=float(w["end"] if isinstance(w, dict) else w.end),
            word=str(w["word"] if isinstance(w, dict) else w.word).strip(),
        )
        for w in raw_words
    ]

    duration = getattr(response, "duration", None)

    user_id = extract_user_id(auth)
    schedule_token_log_task(
        background_tasks,
        tenant_id,
        user_id,
        ai_settings.ModelName,
        "/api/v1/transcription",
        0,
        0,
        audio_duration_seconds=float(duration) if duration is not None else None,
        pipeline_run_id=request.pipeline_run_id,
    )

    return TranscriptionResponse(
        text=getattr(response, "text", "") or "",
        language=getattr(response, "language", None),
        duration=duration,
        segments=segments,
        words=words,
        no_speech_prob_threshold=ai_settings.NoSpeechProbThreshold,
    )
