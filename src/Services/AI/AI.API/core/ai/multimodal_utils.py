"""
core/ai/multimodal_utils.py — Multimodal content block utilities.

Handles raw-byte retrieval from FileManager URLs, MIME classification,
Base64 encoding, and OpenAI-compatible content block assembly.

LiteLLM's adapter layer translates these standard blocks into the
proprietary formats required by Claude, Gemini, Qwen, etc.
"""
import base64
import mimetypes
from typing import Any, Dict, List, Optional, Tuple

import httpx

# ---------------------------------------------------------------------------
# MIME type sets
# ---------------------------------------------------------------------------

IMAGE_MIME_TYPES: frozenset[str] = frozenset({
    "image/png",
    "image/jpeg",
    "image/jpg",
    "image/gif",
    "image/webp",
    "image/bmp",
    "image/tiff",
})

AUDIO_MIME_TYPES: frozenset[str] = frozenset({
    "audio/mpeg",
    "audio/mp3",
    "audio/wav",
    "audio/wave",
    "audio/x-wav",
    "audio/ogg",
    "audio/webm",
    "audio/mp4",
    "audio/aac",
    "audio/flac",
    "audio/x-flac",
})

DOCUMENT_MIME_TYPES: frozenset[str] = frozenset({
    "application/pdf",
    "application/msword",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "text/plain",
    "text/csv",
    "text/markdown",
    "text/html",
})

# Maps MIME types to the format string expected by LiteLLM's input_audio block.
AUDIO_MIME_TO_FORMAT: dict[str, str] = {
    "audio/mpeg": "mp3",
    "audio/mp3": "mp3",
    "audio/wav": "wav",
    "audio/wave": "wav",
    "audio/x-wav": "wav",
    "audio/ogg": "ogg",
    "audio/webm": "webm",
    "audio/mp4": "mp4",
    "audio/aac": "aac",
    "audio/flac": "flac",
    "audio/x-flac": "flac",
}

# ---------------------------------------------------------------------------
# Provider capability sets
# ---------------------------------------------------------------------------

# Providers whose models can process image_url content blocks via LiteLLM.
PROVIDERS_SUPPORTING_VISION: frozenset[str] = frozenset({
    "openai",
    "azure",
    "anthropic",
    "gemini",
    "groq",
    "mistral",
    "ollama",
})

# Providers whose models can process input_audio content blocks via LiteLLM.
PROVIDERS_SUPPORTING_AUDIO: frozenset[str] = frozenset({
    "openai",
    "gemini",
})

# Raw provider strings (before alias normalisation) that use Qwen's audio strategy:
# pass the audio URL directly in an input_audio block instead of downloading and
# base64-encoding the file.  The Dashscope compatible-mode endpoint accepts
# input_audio with a real HTTP URL in the 'data' field:
#   {"type": "input_audio", "input_audio": {"data": "https://...", "format": "mp3"}}
QWEN_RAW_PROVIDERS: frozenset[str] = frozenset({
    "qwen",
    "qwenai",
    "alibaba",
    "dashscope",
})


# ---------------------------------------------------------------------------
# Audio format resolver
# ---------------------------------------------------------------------------

def resolve_audio_format(raw_provider: str, provider_normalized: str) -> str:
    """
    Return the audio encoding strategy for the given provider combination.

    Return values
    -------------
    "input_audio"   — OpenAI-compatible block.
                      Used by: OpenAI, Gemini (LiteLLM translates to inline_data),
                      Azure, Groq, and any other OpenAI-aliased provider.
                      Block shape: {"type": "input_audio",
                                    "input_audio": {"data": "<b64>", "format": "mp3"}}

    "audio_url"     — Qwen omni strategy: pass a real HTTP URL in an input_audio
                      block (avoids downloading the file server-side).
                      Block shape: {"type": "input_audio",
                                    "input_audio": {"data": "<https-url>", "format": "mp3"}}

    "text_fallback" — Provider has no native audio API (e.g. Anthropic/Claude).
                      A text description of the file is produced instead so the
                      message remains valid without raising an HTTP 400.
    """
    if raw_provider.strip().lower() in QWEN_RAW_PROVIDERS:
        return "audio_url"
    if provider_normalized.strip().lower() == "anthropic":
        return "text_fallback"
    # Default: OpenAI input_audio — covers openai, gemini, azure, groq, etc.
    return "input_audio"


# ---------------------------------------------------------------------------
# Raw byte fetcher
# ---------------------------------------------------------------------------

async def fetch_file_bytes(url: str) -> bytes:
    """Download raw bytes from a URL. Raises httpx.HTTPStatusError on non-2xx responses."""
    async with httpx.AsyncClient(follow_redirects=True, timeout=30.0) as client:
        response = await client.get(url)
        response.raise_for_status()
        return response.content


async def fetch_file_bytes_with_fallback(primary_url: str, fallback_url: Optional[str]) -> bytes:
    """Attempt to download from primary_url; on any error fall back to fallback_url."""
    try:
        return await fetch_file_bytes(primary_url)
    except Exception:
        if fallback_url and fallback_url != primary_url:
            return await fetch_file_bytes(fallback_url)
        raise


# ---------------------------------------------------------------------------
# MIME resolution
# ---------------------------------------------------------------------------

def resolve_mime_type(file_metadata: dict) -> str:
    """Derive the MIME type from FileManager metadata, falling back to extension guessing."""
    mime = (
        file_metadata.get("content_type")
        or file_metadata.get("contentType")
        or file_metadata.get("mimeType")
        or file_metadata.get("mime_type")
    )
    if mime:
        return str(mime).strip().lower()

    extension = str(file_metadata.get("extension") or "").lstrip(".").lower()
    guessed, _ = mimetypes.guess_type(f"file.{extension}")
    return guessed or "application/octet-stream"


def classify_media_type(mime_type: str) -> str:
    """Return 'image', 'audio', 'document', or 'unknown' for the given MIME type."""
    if mime_type in IMAGE_MIME_TYPES:
        return "image"
    if mime_type in AUDIO_MIME_TYPES:
        return "audio"
    if mime_type in DOCUMENT_MIME_TYPES:
        return "document"
    return "unknown"


# ---------------------------------------------------------------------------
# Content block builders
# ---------------------------------------------------------------------------

def build_image_block(data: bytes, mime_type: str) -> dict[str, Any]:
    """Build an OpenAI-compatible image_url content block from raw bytes."""
    encoded = base64.b64encode(data).decode("ascii")
    return {
        "type": "image_url",
        "image_url": {"url": f"data:{mime_type};base64,{encoded}"},
    }


def build_audio_block(data: bytes, mime_type: str) -> dict[str, Any]:
    """Build an OpenAI-compatible input_audio content block from raw bytes."""
    fmt = AUDIO_MIME_TO_FORMAT.get(mime_type, "mp3")
    encoded = base64.b64encode(data).decode("ascii")
    return {
        "type": "input_audio",
        "input_audio": {"data": encoded, "format": fmt},
    }


def build_audio_url_block(url: str, mime_type: str = "audio/mpeg") -> dict[str, Any]:
    """Build a Qwen-compatible input_audio content block from a real HTTP URL.

    Qwen omni models (Dashscope compatible-mode endpoint) accept audio via the
    standard input_audio block where 'data' is either a URL or base64 data.
    When an external URL is available, passing it directly avoids an expensive
    download round-trip — Dashscope fetches the audio server-side during inference.
    """
    fmt = AUDIO_MIME_TO_FORMAT.get(mime_type, "mp3")
    return {
        "type": "input_audio",
        "input_audio": {"data": url, "format": fmt},
    }


def build_document_text_block(file_metadata: dict) -> dict[str, Any]:
    """Build a text content block for documents — URL + filename as context fallback."""
    name = file_metadata.get("name", "document")
    extension = file_metadata.get("extension", "")
    url = (
        file_metadata.get("external_url")
        or file_metadata.get("externalUrl")
        or file_metadata.get("url")
        or ""
    )
    text = f"Attached document: {name}{extension}"
    if url:
        text += f" ({url})"
    return {"type": "text", "text": text}


# ---------------------------------------------------------------------------
# Batch processor
# ---------------------------------------------------------------------------

async def build_media_content_blocks(
    files_metadata: List[Dict[str, Any]],
    audio_format: str = "input_audio",
) -> Tuple[List[Dict[str, Any]], bool, bool]:
    """
    Fetch and encode all attached files into provider-compatible content blocks.

    Args:
        files_metadata — List of file metadata dicts from FileManager.
        audio_format   — One of the strings returned by resolve_audio_format():
                         "input_audio"   OpenAI / Gemini / Azure / Groq.
                         "audio_url"     Qwen omni (Dashscope endpoint).
                         "text_fallback" Anthropic/Claude — no native audio;
                                         emits a text description block instead.

    Returns:
        blocks    — List of content block dicts in declaration order.
        has_image — True if at least one image block was produced.
        has_audio — True if at least one *audio* block was produced.
                    False for text_fallback (the file becomes a text block).
    """
    blocks: List[Dict[str, Any]] = []
    has_image = False
    has_audio = False

    for meta in files_metadata:
        if not isinstance(meta, dict):
            continue

        external_url: Optional[str] = (
            meta.get("external_url") or meta.get("externalUrl") or None
        )
        internal_url: Optional[str] = meta.get("url") or None
        primary_url = external_url or internal_url
        if not primary_url:
            continue

        mime_type = resolve_mime_type(meta)
        media_cat = classify_media_type(mime_type)

        # For downloadable media, try external URL first; fall back to internal URL on failure.
        if media_cat == "image":
            raw = await fetch_file_bytes_with_fallback(primary_url, internal_url)
            blocks.append(build_image_block(raw, mime_type))
            has_image = True
        elif media_cat == "audio":
            if audio_format == "text_fallback":
                # Anthropic/Claude has no native audio API — emit a text description
                # so the message stays valid without triggering an HTTP 400.
                blocks.append(build_document_text_block(meta))
            elif audio_format == "audio_url":
                # Qwen omni: pass a real HTTP URL in an input_audio block.
                # Dashscope accepts a URL in the 'data' field, so no download needed.
                # Prefer external/CDN URL; fall back to internal URL.
                audio_url = external_url or internal_url
                if not audio_url:
                    blocks.append(build_document_text_block(meta))
                else:
                    blocks.append(build_audio_url_block(audio_url, mime_type))
                    has_audio = True
            else:  # "input_audio" — OpenAI, Gemini (LiteLLM → inline_data), Azure, Groq
                raw = await fetch_file_bytes_with_fallback(primary_url, internal_url)
                blocks.append(build_audio_block(raw, mime_type))
                has_audio = True
        elif media_cat == "document":
            blocks.append(build_document_text_block(meta))
        # "unknown" MIME types are silently skipped.

    return blocks, has_image, has_audio
