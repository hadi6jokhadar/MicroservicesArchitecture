import pytest
import uuid
from models import AiSystemPrompt

@pytest.mark.asyncio
async def test_get_system_prompts(client, mock_db_session):
    # Mock returning one item
    mock_prompt = AiSystemPrompt(
        Id=uuid.uuid4(),
        TenantId="tenant-001",
        Name="EnhanceText",
        PromptText="Enhance this text:"
    )
    
    mock_db_session.mock_scalars.all.return_value = [mock_prompt]
    
    response = await client.get("/api/v1/prompts/")
    assert response.status_code == 200
    
    data = response.json()
    assert len(data) == 1
    assert data[0]["Name"] == "EnhanceText"

@pytest.mark.asyncio
async def test_create_system_prompt(client, mock_db_session):
    payload = {
        "Name": "Summarizer",
        "PromptText": "Summarize the following:",
        "TenantId": "tenant-001"
    }
    
    response = await client.post("/api/v1/prompts/", json=payload)
    assert response.status_code == 200
    
    data = response.json()
    assert data["Name"] == "Summarizer"
    assert data["PromptText"] == "Summarize the following:"
    assert "Id" in data
    
    # Verify DB commits
    assert mock_db_session.add.called
    assert mock_db_session.commit.called
    assert mock_db_session.refresh.called


@pytest.mark.asyncio
async def test_create_system_prompt_without_tenant_context(client, mock_db_session):
    payload = {
        "Name": "GlobalPrompt",
        "PromptText": "Use neutral tone"
    }

    response = await client.post("/api/v1/prompts/", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["Name"] == "GlobalPrompt"
    assert "TenantId" in data
