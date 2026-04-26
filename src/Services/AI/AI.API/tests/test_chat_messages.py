import uuid
from datetime import datetime, timezone

import pytest

from models import AiChatMessage


@pytest.mark.asyncio
async def test_list_chat_messages_returns_data(client, mock_db_session):
    session_id = uuid.uuid4()
    message = AiChatMessage(
        Id=uuid.uuid4(),
        SessionId=session_id,
        Role="user",
        Content="Hello",
        PromptTokens=12,
        CompletionTokens=5,
        CreatedAt=datetime.now(timezone.utc),
    )
    mock_db_session.mock_scalars.all.return_value = [message]

    response = await client.get(f"/api/v1/chat-messages/?session_id={session_id}")
    assert response.status_code == 200

    data = response.json()
    assert len(data) == 1
    assert data[0]["Role"] == "user"
    assert data[0]["SessionId"] == str(session_id)


@pytest.mark.asyncio
async def test_list_chat_messages_validates_role(client):
    response = await client.get("/api/v1/chat-messages/?role=invalid")
    assert response.status_code == 400
