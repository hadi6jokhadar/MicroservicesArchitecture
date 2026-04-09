import enum

class ModelTypeEnum(str, enum.Enum):
    Text = "Text"
    Vision = "Vision"
    Audio = "Audio"
    Embedding = "Embedding"
    ImageGeneration = "ImageGeneration"
