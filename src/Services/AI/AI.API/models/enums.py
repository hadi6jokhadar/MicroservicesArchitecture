import enum

class ModelTypeEnum(str, enum.Enum):
    Text = "Text"
    Vision = "Vision"
    Audio = "Audio"
    Embedding = "Embedding"
    ImageGeneration = "ImageGeneration"


class AudioDataModeEnum(str, enum.Enum):
    """Controls how audio file bytes are delivered to the model API.

    Auto   — Let the provider auto-detection logic decide (default).
             Qwen → URL, OpenAI/Gemini → Base64, Claude → text fallback.
    Url    — Pass a real HTTP URL in an audio_url block.
             Required for Qwen omni; also works with any provider that can
             fetch audio URLs server-side.
    Base64 — Download bytes and encode as Base64 in an input_audio block.
             Required for OpenAI and Gemini; works for providers with no
             server-side fetch capability.
    """
    Auto   = "Auto"
    Url    = "Url"
    Base64 = "Base64"
