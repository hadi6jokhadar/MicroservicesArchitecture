import pytest
import uuid
from models import AiProviderSettings, ModelTypeEnum

@pytest.mark.asyncio
async def test_get_settings(client, mock_db_session, mocker):
    # Mock returning one item
    mock_setting = AiProviderSettings(
        Id=uuid.uuid4(),
        Key="default",
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
        "Key": "default",
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
        "Key": "default",
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
        "Key": "default",
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


@pytest.mark.asyncio
async def test_get_setting_by_id(client, mock_db_session):
    setting_id = uuid.uuid4()
    mock_setting = AiProviderSettings(
        Id=setting_id,
        Key="default",
        TenantId="tenant-001",
        ModelType=ModelTypeEnum.Text,
        Provider="OpenAI",
        ApiKey="test-key-get-one",
        ModelName="gpt-4.1"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = mock_setting

    response = await client.get(f"/api/v1/settings/{setting_id}")
    assert response.status_code == 200

    data = response.json()
    assert data["Id"] == str(setting_id)
    assert data["ModelName"] == "gpt-4.1"


@pytest.mark.asyncio
async def test_get_setting_by_key(client, mock_db_session):
    setting_id = uuid.uuid4()
    mock_setting = AiProviderSettings(
        Id=setting_id,
        Key="default",
        TenantId="tenant-001",
        ModelType=ModelTypeEnum.Text,
        Provider="OpenAI",
        ApiKey="test-key-by-key",
        ModelName="gpt-4o-mini"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = mock_setting

    response = await client.get("/api/v1/settings/by-key/default")
    assert response.status_code == 200
    assert response.json()["Id"] == str(setting_id)


@pytest.mark.asyncio
async def test_get_setting_returns_404_when_not_found(client, mock_db_session):
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = None

    response = await client.get(f"/api/v1/settings/{uuid.uuid4()}")
    assert response.status_code == 404
    assert response.json()["detail"] == "AI provider setting not found."


@pytest.mark.asyncio
async def test_update_setting(client, mock_db_session):
    setting_id = uuid.uuid4()
    existing_setting = AiProviderSettings(
        Id=setting_id,
        Key="default",
        TenantId="tenant-001",
        ModelType=ModelTypeEnum.Text,
        Provider="OpenAI",
        ApiKey="old-key",
        ModelName="gpt-4o-mini"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = existing_setting

    payload = {
        "Key": "default-updated",
        "ModelType": "Text",
        "Provider": "AzureOpenAI",
        "ApiKey": "new-key",
        "ModelName": "gpt-4.1-mini"
    }

    response = await client.put(f"/api/v1/settings/{setting_id}", json=payload)
    assert response.status_code == 200

    data = response.json()
    assert data["Provider"] == "AzureOpenAI"
    assert data["ApiKey"] == "new-key"
    assert data["TenantId"] is not None
    assert mock_db_session.commit.called
    assert mock_db_session.refresh.called


@pytest.mark.asyncio
async def test_update_setting_in_global_scope_keeps_existing_tenant(client, mock_db_session, monkeypatch):
    async def override_get_tenant_id_none():
        return None

    from api.dependencies import get_tenant_id
    from main import app

    existing_setting = AiProviderSettings(
        Id=uuid.uuid4(),
        Key="default",
        TenantId=None,
        ModelType=ModelTypeEnum.Text,
        Provider="OpenAI",
        ApiKey="global-key",
        ModelName="gpt-4o"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = existing_setting
    app.dependency_overrides[get_tenant_id] = override_get_tenant_id_none

    payload = {
        "Key": "default",
        "ModelType": "Text",
        "Provider": "OpenAI",
        "ApiKey": "updated-global-key",
        "ModelName": "gpt-4.1"
    }

    response = await client.put(f"/api/v1/settings/{existing_setting.Id}", json=payload)

    app.dependency_overrides.pop(get_tenant_id, None)

    assert response.status_code == 200
    assert response.json()["TenantId"] is None


@pytest.mark.asyncio
async def test_delete_setting(client, mock_db_session):
    existing_setting = AiProviderSettings(
        Id=uuid.uuid4(),
        Key="default",
        TenantId="tenant-001",
        ModelType=ModelTypeEnum.Text,
        Provider="OpenAI",
        ApiKey="test-key-delete",
        ModelName="gpt-4o"
    )
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = existing_setting

    response = await client.delete(f"/api/v1/settings/{existing_setting.Id}")
    assert response.status_code == 204
    assert mock_db_session.delete.called
    assert mock_db_session.commit.called


@pytest.mark.asyncio
async def test_delete_setting_returns_404_when_not_found(client, mock_db_session):
    mock_db_session.mock_execute_result.scalar_one_or_none.return_value = None

    response = await client.delete(f"/api/v1/settings/{uuid.uuid4()}")
    assert response.status_code == 404
    assert response.json()["detail"] == "AI provider setting not found."
