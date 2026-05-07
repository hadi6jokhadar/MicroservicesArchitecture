import pytest
from unittest.mock import AsyncMock

from models import ModelTypeEnum


@pytest.mark.asyncio
async def test_embedding_uses_local_baai_bge_m3_when_setting_matches(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Key = "BAAI-bge-m3"
    mock_setting.Provider = "BAAI"
    mock_setting.ModelName = "bge-m3"
    mock_setting.ApiKey = "localhost"
    mock_setting.ApiBaseUrl = "localhost"
    mock_setting.ModelType = ModelTypeEnum.Embedding
    mock_db_session.mock_scalars.first.return_value = mock_setting

    mock_local_embed = mocker.patch(
        "api.routes.embedding.embed_with_local_bge_m3",
        return_value=([0.11, 0.22, 0.33], "BAAI/bge-m3"),
    )
    mock_aembedding = mocker.patch("api.routes.embedding.aembedding", new_callable=AsyncMock)
    mocker.patch("api.routes.embedding.schedule_token_log_task")

    response = await client.post(
        "/api/v1/embedding",
        json={"settingsKey": "BAAI-bge-m3", "text": "test input"},
    )

    assert response.status_code == 200
    body = response.json()
    assert body["embedding"] == [0.11, 0.22, 0.33]
    assert body["model"] == "BAAI/bge-m3"
    mock_local_embed.assert_called_once_with("test input")
    assert mock_aembedding.await_count == 0


@pytest.mark.asyncio
async def test_embedding_keeps_litellm_path_for_non_local_settings(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Key = "some-key"
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "text-embedding-3-small"
    mock_setting.ApiKey = "fake-key"
    mock_setting.ApiBaseUrl = None
    mock_setting.ModelType = ModelTypeEnum.Embedding
    mock_db_session.mock_scalars.first.return_value = mock_setting

    class MockUsage:
        prompt_tokens = 12

    class MockResponse:
        data = [{"embedding": [0.1, 0.2]}]
        model = "openai/text-embedding-3-small"
        usage = MockUsage()

    async def mock_aembedding(*args, **kwargs):
        return MockResponse()

    mocker.patch("api.routes.embedding.aembedding", side_effect=mock_aembedding)
    mocker.patch("api.routes.embedding.schedule_token_log_task")

    response = await client.post(
        "/api/v1/embedding",
        json={"settingsKey": "some-key", "text": "hello"},
    )

    assert response.status_code == 200
    body = response.json()
    assert body["embedding"] == [0.1, 0.2]
    assert body["model"] == "openai/text-embedding-3-small"


@pytest.mark.asyncio
async def test_embedding_returns_500_when_local_baai_fails(client, mock_db_session, mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Key = "BAAI-bge-m3"
    mock_setting.Provider = "BAAI"
    mock_setting.ModelName = "bge-m3"
    mock_setting.ApiKey = "localhost"
    mock_setting.ApiBaseUrl = "localhost"
    mock_setting.ModelType = ModelTypeEnum.Embedding
    mock_db_session.mock_scalars.first.return_value = mock_setting

    mocker.patch(
        "api.routes.embedding.embed_with_local_bge_m3",
        side_effect=RuntimeError("local model unavailable"),
    )
    mocker.patch("api.routes.embedding.schedule_token_log_task")

    response = await client.post(
        "/api/v1/embedding",
        json={"settingsKey": "BAAI-bge-m3", "text": "test input"},
    )

    assert response.status_code == 500
    assert "Local BAAI embedding failed" in response.json()["detail"]
