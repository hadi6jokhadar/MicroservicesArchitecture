from __future__ import annotations

import threading
from typing import Any

_LOCAL_BGE_PROVIDER = "baai"
_LOCAL_BGE_MODEL_NAMES = {"bge-m3", "baai/bge-m3"}

_model_lock = threading.Lock()
_bge_m3_model = None


def is_local_bge_m3_setting(settings: Any) -> bool:
    """Return True when settings should use local BAAI bge-m3 inference."""
    provider = str(getattr(settings, "Provider", "") or "").strip().lower()
    model_name = str(getattr(settings, "ModelName", "") or "").strip().lower()
    return provider == _LOCAL_BGE_PROVIDER and model_name in _LOCAL_BGE_MODEL_NAMES


def embed_with_local_bge_m3(text: str) -> tuple[list[float], str]:
    """Generate an embedding with the local BAAI bge-m3 model."""
    model = _get_bge_m3_model()
    encoded = model.encode(text, convert_to_numpy=True, normalize_embeddings=False)
    vector = encoded.tolist() if hasattr(encoded, "tolist") else list(encoded)
    return [float(value) for value in vector], "BAAI/bge-m3"


def _get_bge_m3_model():
    global _bge_m3_model
    if _bge_m3_model is not None:
        return _bge_m3_model

    with _model_lock:
        if _bge_m3_model is None:
            from sentence_transformers import SentenceTransformer  # type: ignore

            _bge_m3_model = SentenceTransformer("BAAI/bge-m3")

    return _bge_m3_model
