import pytest

from core.ai.chat_workflow import run_chat_orchestration
from core.ai.schemas import ChatRequest


@pytest.mark.asyncio
async def test_run_chat_orchestration_creates_user_message_when_only_files_present(mocker):
    mock_setting = mocker.MagicMock()
    mock_setting.Provider = "OpenAI"
    mock_setting.ModelName = "gpt-4o"
    mock_setting.ApiKey = "test-key"
    mock_setting.AudioDataMode = None

    async def mock_get_files_by_ids(file_ids, tenant_id):
        return [{"id": 1, "name": "your-nasheed", "extension": ".mp3"}]

    async def mock_build_media_content_blocks(files_metadata, audio_format):
        return (
            [{"type": "audio", "audio_url": "https://your-bucket/your-nasheed.mp3"}],
            False,
            True,
        )

    mocker.patch(
        "core.ai.chat_workflow.file_manager_client.get_files_by_ids",
        side_effect=mock_get_files_by_ids,
    )
    mocker.patch(
        "core.ai.chat_workflow.build_media_content_blocks",
        side_effect=mock_build_media_content_blocks,
    )

    request = ChatRequest(
        settings_key="default",
        system_prompt_key="System-Only",
        messages=[],
        file_ids=[1],
    )

    state = await run_chat_orchestration(
        request=request,
        ai_settings=mock_setting,
        system_prompt="You are concise",
        tenant_id="tenant-1",
    )

    assert state["litellm_messages"][0] == {"role": "system", "content": "You are concise"}
    assert state["litellm_messages"][1]["role"] == "user"
    assert state["litellm_messages"][1]["content"] == [
        {"type": "audio", "audio_url": "https://your-bucket/your-nasheed.mp3"}
    ]
