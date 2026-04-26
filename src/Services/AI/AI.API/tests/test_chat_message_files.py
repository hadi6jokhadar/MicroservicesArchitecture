import uuid

import pytest

from models import AiChatMessageFile


@pytest.mark.asyncio
async def test_list_chat_message_files_returns_data(client, mock_db_session):
    message_id = uuid.uuid4()
    file_id = uuid.uuid4()
    message_file = AiChatMessageFile(
        MessageId=message_id,
        FileId=file_id,
    )
    mock_db_session.mock_scalars.all.return_value = [message_file]

    response = await client.get(f"/api/v1/chat-message-files/?message_id={message_id}")
    assert response.status_code == 200

    data = response.json()
    assert len(data) == 1
    assert data[0]["MessageId"] == str(message_id)
    assert data[0]["FileId"] == str(file_id)


@pytest.mark.asyncio
async def test_list_chat_message_files_validates_limit(client):
    response = await client.get("/api/v1/chat-message-files/?limit=0")
    assert response.status_code == 400
