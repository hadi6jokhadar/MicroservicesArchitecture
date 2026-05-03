import json
import uuid

import pytest

from core.ai.persistence import persist_messages_background


class _FakeDbSession:
    def __init__(self):
        self.added = []
        self.committed = False

    def add(self, obj):
        self.added.append(obj)

    async def commit(self):
        self.committed = True


class _FakeSessionContext:
    def __init__(self, db):
        self._db = db

    async def __aenter__(self):
        return self._db

    async def __aexit__(self, exc_type, exc, tb):
        return False


@pytest.mark.asyncio
async def test_persist_messages_background_serializes_structured_content(mocker):
    fake_db = _FakeDbSession()
    mocker.patch("core.ai.persistence.AsyncSessionFactory", return_value=_FakeSessionContext(fake_db))

    await persist_messages_background(
        session_id=uuid.uuid4(),
        model_input_messages=[
            {"role": "system", "content": "system prompt"},
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": "analyze this file"},
                    {"type": "audio", "audio_url": "https://your-bucket/your-nasheed.mp3"},
                ],
            },
        ],
        assistant_content={"summary": "processed"},
        prompt_tokens=42,
        completion_tokens=9,
    )

    assert fake_db.committed is True
    assert len(fake_db.added) == 3

    system_row = fake_db.added[0]
    user_row = fake_db.added[1]
    assistant_row = fake_db.added[2]

    assert system_row.Role == "system"
    assert system_row.Content == "system prompt"
    assert system_row.PromptTokens == 0

    assert user_row.Role == "user"
    assert json.loads(user_row.Content) == [
        {"type": "text", "text": "analyze this file"},
        {"type": "audio", "audio_url": "https://your-bucket/your-nasheed.mp3"},
    ]
    assert user_row.PromptTokens == 42

    assert assistant_row.Role == "assistant"
    assert json.loads(assistant_row.Content) == {"summary": "processed"}
    assert assistant_row.CompletionTokens == 9
