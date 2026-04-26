import uuid
from datetime import datetime, timezone

import pytest

from models import AiTokenUsageLog


@pytest.mark.asyncio
async def test_list_token_usage_logs_returns_data(client, mock_db_session):
    usage_log = AiTokenUsageLog(
        Id=uuid.uuid4(),
        TenantId="tenant-test",
        UserId=123,
        ModelName="gpt-4o",
        PromptTokens=20,
        CompletionTokens=10,
        TotalTokens=30,
        Endpoint="/api/v1/chat/stream",
        CreatedAt=datetime.now(timezone.utc),
    )
    mock_db_session.mock_scalars.all.return_value = [usage_log]

    response = await client.get("/api/v1/token-usage-logs/")
    assert response.status_code == 200

    data = response.json()
    assert len(data) == 1
    assert data[0]["ModelName"] == "gpt-4o"
    assert data[0]["TotalTokens"] == 30


@pytest.mark.asyncio
async def test_list_token_usage_logs_validates_limit(client):
    response = await client.get("/api/v1/token-usage-logs/?limit=0")
    assert response.status_code == 400
