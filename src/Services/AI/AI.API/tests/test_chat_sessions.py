import uuid
from datetime import datetime, timezone

import pytest

from models import AiChatSession


@pytest.mark.asyncio
async def test_list_chat_sessions_returns_data(client, mock_db_session):
    session = AiChatSession(
        Id=uuid.uuid4(),
        TenantId="tenant-test",
        UserId=123,
        Title="Support session",
        CreatedAt=datetime.now(timezone.utc),
    )
    mock_db_session.mock_scalars.all.return_value = [session]

    response = await client.get("/api/v1/chat-sessions/")
    assert response.status_code == 200

    data = response.json()
    assert len(data) == 1
    assert data[0]["TenantId"] == "tenant-test"
    assert data[0]["UserId"] == 123


@pytest.mark.asyncio
async def test_list_chat_sessions_validates_limit(client):
    response = await client.get("/api/v1/chat-sessions/?limit=501")
    assert response.status_code == 400
