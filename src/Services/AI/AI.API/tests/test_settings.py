import pytest
import uuid
from models import AiProviderSettings, ModelTypeEnum

@pytest.mark.asyncio
async def test_get_settings(client, mock_db_session, mocker):
    # Mock returning one item
    mock_setting = AiProviderSettings(
        Id=uuid.uuid4(),
        TenantId="tenant-001",
        ModelType=ModelTypeEnum.Text,
        Provider="OpenAI",
        ApiKey="test-key-get",
        ModelName="gpt-4o"
    )
    
    # MagicMock for scalars().all() -> returns our list
    mock_db_session.mock_scalars.all.return_value = [mock_setting]
    
    response = await client.get("/api/v1/settings/")
    assert response.status_code == 200
    
    data = response.json()
    assert len(data) == 1
    assert data[0]["Provider"] == "OpenAI"

@pytest.mark.asyncio
async def test_create_setting(client, mock_db_session):
    payload = {
        "ModelType": "Text",
        "Provider": "Azure",
        "ApiKey": "test-azure-key",
        "ModelName": "gpt-35-turbo",
        "TenantId": "tenant-001"
    }
    
    # We don't strictly need to mock the returning element if our router
    # returns the newly created object which got added to db.
    response = await client.post("/api/v1/settings/", json=payload)
    assert response.status_code == 200
    
    data = response.json()
    assert data["Provider"] == "Azure"
    assert data["ModelName"] == "gpt-35-turbo"
    assert "Id" in data
    
    # Verify DB commits
    assert mock_db_session.add.called
    assert mock_db_session.commit.called
    assert mock_db_session.refresh.called


@pytest.mark.asyncio
async def test_create_setting_without_tenant_context(client, mock_db_session):
    payload = {
        "ModelType": "Text",
        "Provider": "OpenAI",
        "ApiKey": "test-openai-key",
        "ModelName": "gpt-4o-mini"
    }

    response = await client.post("/api/v1/settings/", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["Provider"] == "OpenAI"
    assert "TenantId" in data


@pytest.mark.asyncio
async def test_create_setting_accepts_string_tenant_id(client, mock_db_session):
    payload = {
        "ModelType": "Text",
        "Provider": "OpenAI",
        "ApiKey": "test-openai-key",
        "ModelName": "gpt-4o-mini",
        "TenantId": "ihsandev"
    }

    response = await client.post("/api/v1/settings/", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["TenantId"] == "ihsandev"
