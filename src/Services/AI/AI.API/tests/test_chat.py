import pytest
import uuid
from pydantic import ValidationError
from unittest.mock import AsyncMock

@pytest.mark.asyncio
async def test_chat_stream_requires_settings(client, mock_db_session, mocker):
    # If no setting exists for the provided key, the endpoint returns 404.
    mock_db_session.mock_scalars.first.return_value = None

    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hello"}]
    }
    
    response = await client.post("/api/v1/chat/stream", json=payload)
    assert response.status_code == 404
    assert "not found" in response.json()["detail"]

@pytest.mark.asyncio
async def test_chat_stream_success(client, mock_db_session, mocker):
    # Mock finding a setting
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o"
    mock_setting.ApiKey = "fake-key"
    mock_db_session.mock_scalars.first.return_value = mock_setting
    
    called_kwargs = {}

    # Mock LiteLLM acompletion
    async def mock_acompletion(*args, **kwargs):
        called_kwargs.update(kwargs)

        class MockChoice:
            class MockDelta:
                content = "Hello there!"
            delta = MockDelta()
            
        class MockChunk:
            choices = [MockChoice()]
            
        yield MockChunk()

    mocker.patch("api.routes.chat.acompletion", side_effect=mock_acompletion)
    mocker.patch("api.routes.chat.persist_messages_background", new_callable=AsyncMock)
    mocker.patch("api.routes.chat.log_token_usage_background", new_callable=AsyncMock)

    # We also need to prevent background task issues or explicitly check
    # But BackgroundTasks in FastAPI TestClient are executed synchronously after response.
    # So our DB mock will be called.
    
    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hi"}]
    }
    
    # We can't easily parse Server Sent Events with standard json() 
    # so we will check text lines
    response = await client.post("/api/v1/chat/stream", json=payload)
    assert response.status_code == 200
    
    content = response.text
    # Check if SSE formatted properly
    assert "data: " in content
    assert "Hello there!" in content
    assert "[DONE]" in content
    assert called_kwargs.get("model") == "openai/gpt-4o"


def test_build_litellm_model_handles_already_prefixed_model():
    from api.routes.chat import build_litellm_model

    model = build_litellm_model("OpenAI", "openai/gpt-4o")
    assert model == "openai/gpt-4o"


@pytest.mark.asyncio
async def test_chat_stream_rejects_empty_messages(client):
    response = await client.post("/api/v1/chat/stream", json={"settings_key": "default", "messages": []})
    assert response.status_code == 400


def test_chat_message_role_validation():
    from api.routes.chat import ChatMessage

    with pytest.raises(ValidationError):
        ChatMessage(role="invalid-role", content="Hello")


@pytest.mark.asyncio
async def test_run_chat_orchestration_builds_payload_and_model(mocker):
    from api.routes.chat import ChatRequest, run_chat_orchestration

    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o-mini"
    mock_setting.ApiKey = "test-key"

    request = ChatRequest(settings_key="default", messages=[{"role": "user", "content": "Hello"}])
    state = await run_chat_orchestration(request, mock_setting)

    assert state["litellm_model"] == "openai/gpt-4o-mini"
    assert state["litellm_messages"] == [{"role": "user", "content": "Hello"}]
    assert state["api_key"] == "test-key"
