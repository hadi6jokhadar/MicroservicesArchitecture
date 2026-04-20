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


@pytest.mark.asyncio
async def test_get_system_prompt_by_id(client, mock_db_session):
    prompt_id = uuid.uuid4()
    mock_prompt = AiSystemPrompt(
        Id=prompt_id,
        TenantId="tenant-001",
        Name="EnhanceText",
        PromptText="Enhance this text:"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = mock_prompt

    response = await client.get(f"/api/v1/prompts/{prompt_id}")
    assert response.status_code == 200

    data = response.json()
    assert data["Id"] == str(prompt_id)
    assert data["Name"] == "EnhanceText"


@pytest.mark.asyncio
async def test_get_system_prompt_returns_404_when_not_found(client, mock_db_session):
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = None

    response = await client.get(f"/api/v1/prompts/{uuid.uuid4()}")
    assert response.status_code == 404
    assert response.json()["detail"] == "System prompt not found."


@pytest.mark.asyncio
async def test_update_system_prompt(client, mock_db_session):
    prompt_id = uuid.uuid4()
    existing_prompt = AiSystemPrompt(
        Id=prompt_id,
        TenantId="tenant-001",
        Name="Summarizer",
        PromptText="Summarize this."
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = existing_prompt

    payload = {
        "Name": "Translator",
        "PromptText": "Translate to Arabic."
    }

    response = await client.put(f"/api/v1/prompts/{prompt_id}", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["Name"] == "Translator"
    assert data["PromptText"] == "Translate to Arabic."
    assert data["TenantId"] is not None
    assert mock_db_session.commit.called
    assert mock_db_session.refresh.called


@pytest.mark.asyncio
async def test_update_system_prompt_in_global_scope_keeps_existing_tenant(client, mock_db_session):
    async def override_get_tenant_id_none():
        return None

    from api.dependencies import get_tenant_id
    from main import app

    existing_prompt = AiSystemPrompt(
        Id=uuid.uuid4(),
        TenantId=None,
        Name="GlobalPrompt",
        PromptText="Use neutral tone"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = existing_prompt
    app.dependency_overrides[get_tenant_id] = override_get_tenant_id_none

    payload = {
        "Name": "GlobalPromptUpdated",
        "PromptText": "Use formal tone"
    }

    response = await client.put(f"/api/v1/prompts/{existing_prompt.Id}", json=payload)

    app.dependency_overrides.pop(get_tenant_id, None)

    assert response.status_code == 200
    assert response.json()["TenantId"] is None


@pytest.mark.asyncio
async def test_delete_system_prompt(client, mock_db_session):
    existing_prompt = AiSystemPrompt(
        Id=uuid.uuid4(),
        TenantId="tenant-001",
        Name="DeletePrompt",
        PromptText="Delete me"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = existing_prompt

    response = await client.delete(f"/api/v1/prompts/{existing_prompt.Id}")
    assert response.status_code == 204
    assert mock_db_session.delete.called
    assert mock_db_session.commit.called


@pytest.mark.asyncio
async def test_delete_system_prompt_returns_404_when_not_found(client, mock_db_session):
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = None

    response = await client.delete(f"/api/v1/prompts/{uuid.uuid4()}")
    assert response.status_code == 404
    assert response.json()["detail"] == "System prompt not found."
