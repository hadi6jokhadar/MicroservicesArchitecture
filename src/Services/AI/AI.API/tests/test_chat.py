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


@pytest.mark.asyncio
async def test_chat_single_response_success(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o"
    mock_setting.ApiKey = "fake-key"
    mock_db_session.mock_scalars.first.return_value = mock_setting

    called_kwargs = {}

    class MockMessage:
        content = "Single response"

    class MockChoice:
        message = MockMessage()

    class MockUsage:
        prompt_tokens = 12
        completion_tokens = 7

    class MockCompletionResponse:
        choices = [MockChoice()]
        usage = MockUsage()

    async def mock_acompletion(*args, **kwargs):
        called_kwargs.update(kwargs)
        return MockCompletionResponse()

    mocker.patch("api.routes.chat.acompletion", side_effect=mock_acompletion)
    mocker.patch("api.routes.chat.persist_messages_background", new_callable=AsyncMock)
    mocker.patch("api.routes.chat.log_token_usage_background", new_callable=AsyncMock)

    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hi"}],
    }

    response = await client.post("/api/v1/chat/single", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["content"] == "Single response"
    assert data["prompt_tokens"] == 12
    assert data["completion_tokens"] == 7
    assert data["total_tokens"] == 19
    assert called_kwargs.get("model") == "openai/gpt-4o"
    assert called_kwargs.get("stream") is False


@pytest.mark.asyncio
async def test_chat_single_response_estimates_tokens_when_usage_missing(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o"
    mock_setting.ApiKey = "fake-key"
    mock_db_session.mock_scalars.first.return_value = mock_setting

    class MockMessage:
        content = "Token fallback content"

    class MockChoice:
        message = MockMessage()

    class MockCompletionResponse:
        choices = [MockChoice()]
        usage = None

    async def mock_acompletion(*args, **kwargs):
        return MockCompletionResponse()

    mocker.patch("api.routes.chat.acompletion", side_effect=mock_acompletion)
    mocker.patch("api.routes.chat.persist_messages_background", new_callable=AsyncMock)
    mocker.patch("api.routes.chat.log_token_usage_background", new_callable=AsyncMock)

    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hi"}],
    }

    response = await client.post("/api/v1/chat/single", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["content"] == "Token fallback content"
    assert data["prompt_tokens"] > 0
    assert data["completion_tokens"] > 0
    assert data["total_tokens"] == data["prompt_tokens"] + data["completion_tokens"]


@pytest.mark.asyncio
async def test_chat_single_response_returns_500_when_provider_fails(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o"
    mock_setting.ApiKey = "fake-key"
    mock_db_session.mock_scalars.first.return_value = mock_setting

    async def mock_acompletion(*args, **kwargs):
        raise RuntimeError("provider down")

    mocker.patch("api.routes.chat.acompletion", side_effect=mock_acompletion)

    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hi"}],
    }

    response = await client.post("/api/v1/chat/single", json=payload)
    assert response.status_code == 500
    assert "provider down" in response.json()["detail"]


@pytest.mark.asyncio
async def test_chat_stream_emits_error_event_when_provider_fails(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o"
    mock_setting.ApiKey = "fake-key"
    mock_db_session.mock_scalars.first.return_value = mock_setting

    async def mock_acompletion(*args, **kwargs):
        raise RuntimeError("stream failure")

    mocker.patch("api.routes.chat.acompletion", side_effect=mock_acompletion)
    persist_mock = mocker.patch("api.routes.chat.persist_messages_background", new_callable=AsyncMock)
    usage_mock = mocker.patch("api.routes.chat.log_token_usage_background", new_callable=AsyncMock)

    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hi"}],
    }

    response = await client.post("/api/v1/chat/stream", json=payload)
    assert response.status_code == 200
    assert "stream failure" in response.text
    assert "[DONE]" not in response.text
    assert persist_mock.await_count == 0
    assert usage_mock.await_count == 0


@pytest.mark.asyncio
async def test_chat_stream_passes_file_context_to_model(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o"
    mock_setting.ApiKey = "fake-key"
    mock_db_session.mock_scalars.first.return_value = mock_setting

    called_kwargs = {}

    async def mock_get_files_by_ids(file_ids, tenant_id):
        return [{"name": "guide", "extension": ".pdf", "url": "https://example.com/guide.pdf"}]

    async def mock_acompletion(*args, **kwargs):
        called_kwargs.update(kwargs)

        class MockChoice:
            class MockDelta:
                content = "done"
            delta = MockDelta()

        class MockChunk:
            choices = [MockChoice()]
            usage = None

        yield MockChunk()

    mocker.patch("api.routes.chat.file_manager_client.get_files_by_ids", side_effect=mock_get_files_by_ids)
    mocker.patch("api.routes.chat.acompletion", side_effect=mock_acompletion)
    mocker.patch("api.routes.chat.persist_messages_background", new_callable=AsyncMock)
    mocker.patch("api.routes.chat.log_token_usage_background", new_callable=AsyncMock)

    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hi"}],
        "file_ids": [1],
    }

    response = await client.post("/api/v1/chat/stream", json=payload)
    assert response.status_code == 200

    messages = called_kwargs["messages"]
    assert len(messages) == 2
    assert "attached to this message" in messages[0]["content"]
    assert messages[1]["content"] == "Hi"


def test_build_file_context_message_returns_none_for_invalid_payload():
    from api.routes.chat import _build_file_context_message

    assert _build_file_context_message([]) is None
    assert _build_file_context_message([{"name": "x", "extension": ".txt"}]) is None


@pytest.mark.asyncio
async def test_run_chat_orchestration_prepends_system_prompt(mocker):
    from api.routes.chat import ChatRequest, run_chat_orchestration

    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o-mini"
    mock_setting.ApiKey = "test-key"

    request = ChatRequest(settings_key="default", messages=[{"role": "user", "content": "Hello"}])
    state = await run_chat_orchestration(request, mock_setting, "You are concise")

    assert state["litellm_messages"][0] == {"role": "system", "content": "You are concise"}
    assert state["litellm_messages"][1] == {"role": "user", "content": "Hello"}


def test_build_litellm_model_handles_already_prefixed_model():
    from api.routes.chat import build_litellm_model

    model = build_litellm_model("OpenAI", "openai/gpt-4o")
    assert model == "openai/gpt-4o"


def test_build_litellm_model_maps_qwenai_to_openai_provider():
    from api.routes.chat import build_litellm_model

    model = build_litellm_model("QwenAI", "qwen3-max")
    assert model == "openai/qwen3-max"


@pytest.mark.asyncio
async def test_chat_stream_qwen_provider_uses_qwen_compatible_api_base(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "QwenAI"
    mock_setting.ModelName = "qwen3-max"
    mock_setting.ApiKey = "qwen-key"
    mock_db_session.mock_scalars.first.return_value = mock_setting

    called_kwargs = {}

    async def mock_acompletion(*args, **kwargs):
        called_kwargs.update(kwargs)

        class MockChoice:
            class MockDelta:
                content = "ok"
            delta = MockDelta()

        class MockChunk:
            choices = [MockChoice()]
            usage = None

        yield MockChunk()

    mocker.patch("api.routes.chat.acompletion", side_effect=mock_acompletion)
    mocker.patch("api.routes.chat.persist_messages_background", new_callable=AsyncMock)
    mocker.patch("api.routes.chat.log_token_usage_background", new_callable=AsyncMock)

    payload = {
        "settings_key": "default",
        "messages": [{"role": "user", "content": "Hello"}],
    }

    response = await client.post("/api/v1/chat/stream", json=payload)
    assert response.status_code == 200
    assert called_kwargs.get("model") == "openai/qwen3-max"
    assert called_kwargs.get("api_base") == "https://dashscope-intl.aliyuncs.com/compatible-mode/v1"


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


@pytest.mark.asyncio
async def test_get_settings_by_key_without_tenant_does_not_force_global_filter(mocker):
    from api.routes.chat import get_settings_by_key

    mock_setting = mocker.MagicMock()

    class _Scalars:
        def first(self):
            return mock_setting

    class _Result:
        def scalars(self):
            return _Scalars()

    class _FakeDbSession:
        captured_stmt = None

        async def execute(self, stmt):
            self.captured_stmt = stmt
            return _Result()

    db = _FakeDbSession()

    setting = await get_settings_by_key("OpenAI-gpt-4.1", None, db)  # type: ignore[arg-type]

    assert setting is mock_setting
    sql = str(db.captured_stmt)
    assert "TenantId IS NULL" not in sql
