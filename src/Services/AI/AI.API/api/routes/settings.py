from fastapi import APIRouter, Depends, HTTPException, Response, status
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from sqlalchemy import or_
from pydantic import BaseModel, ConfigDict, UUID4
from typing import List, Optional
import uuid

from core.database import get_db
from api.dependencies import get_tenant_id, require_superadmin_or_service
from api.attributes import optional_tenant
from models import AiProviderSettings, ModelTypeEnum, AudioDataModeEnum

router = APIRouter()

class ProviderSettingsCreate(BaseModel):
    Key: str
    ModelType: ModelTypeEnum
    Provider: str
    ApiKey: str
    ModelName: str
    TenantId: Optional[str] = None
    ApiBaseUrl: Optional[str] = None
    Temperature: Optional[float] = None
    Stream: Optional[bool] = None
    MaxCompletionTokens: Optional[int] = None
    TopP: Optional[float] = None
    FrequencyPenalty: Optional[float] = None
    PresencePenalty: Optional[float] = None
    Description: Optional[str] = None
    AudioDataMode: Optional[AudioDataModeEnum] = None

class ProviderSettingsResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    Id: UUID4
    Key: str
    ModelType: ModelTypeEnum
    Provider: str
    ApiKey: str
    ModelName: str
    TenantId: Optional[str] = None
    ApiBaseUrl: Optional[str] = None
    Temperature: Optional[float] = None
    Stream: Optional[bool] = None
    MaxCompletionTokens: Optional[int] = None
    TopP: Optional[float] = None
    FrequencyPenalty: Optional[float] = None
    PresencePenalty: Optional[float] = None
    Description: Optional[str] = None
    AudioDataMode: Optional[AudioDataModeEnum] = None


class SettingsScopeFilter(str):
    ALL = "all"
    TENANT = "tenant"
    GLOBAL = "global"


async def _get_scoped_setting(
    setting_id: uuid.UUID,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> Optional[AiProviderSettings]:
    query = select(AiProviderSettings).where(AiProviderSettings.Id == setting_id)
    if tenant_id:
        # If tenant_id is provided, allow both tenant-specific and global settings
        query = query.where(or_(AiProviderSettings.TenantId == tenant_id, AiProviderSettings.TenantId.is_(None)))
    else:
        # If no tenant_id is extracted from headers (superadmin/service), allow ALL settings
        pass

    result = await db.execute(query)
    return result.scalar_one_or_none()

@router.get("/", response_model=List[ProviderSettingsResponse])
@optional_tenant
async def get_settings(
    scope: str = SettingsScopeFilter.ALL,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    normalized_scope = scope.lower()
    if normalized_scope not in {SettingsScopeFilter.ALL, SettingsScopeFilter.TENANT, SettingsScopeFilter.GLOBAL}:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Invalid scope filter. Allowed values: all, tenant, global.",
        )

    query = select(AiProviderSettings)

    if normalized_scope == SettingsScopeFilter.GLOBAL:
        query = query.where(AiProviderSettings.TenantId.is_(None))
    elif normalized_scope == SettingsScopeFilter.TENANT:
        if tenant_id:
            query = query.where(AiProviderSettings.TenantId == tenant_id)
        else:
            query = query.where(AiProviderSettings.TenantId.is_not(None))
    else:
        if tenant_id:
            query = query.where(or_(AiProviderSettings.TenantId == tenant_id, AiProviderSettings.TenantId.is_(None)))

    result = await db.execute(query)
    return result.scalars().all()


@router.get("/by-key/{key}", response_model=Optional[ProviderSettingsResponse])
@optional_tenant
async def get_setting_by_key(
    key: str,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    query = select(AiProviderSettings).where(AiProviderSettings.Key == key)
    if tenant_id:
        query = query.where(
            or_(AiProviderSettings.TenantId == tenant_id, AiProviderSettings.TenantId.is_(None))
        )
    result = await db.execute(query)
    setting = result.scalar_one_or_none()
    return setting


@router.get("/{setting_id}", response_model=ProviderSettingsResponse)
@optional_tenant
async def get_setting(
    setting_id: UUID4,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    setting = await _get_scoped_setting(setting_id, tenant_id, db)
    if setting is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="AI provider setting not found.")

    return setting

@router.post("/", response_model=ProviderSettingsResponse)
@optional_tenant
async def create_setting(
    setting: ProviderSettingsCreate,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    # Depending on auth, might want to restrict this to 'Admin' users only.
    resolved_tenant_id = setting.TenantId
    if resolved_tenant_id is None and tenant_id:
        resolved_tenant_id = tenant_id

    new_setting = AiProviderSettings(
        Key=setting.Key,
        TenantId=resolved_tenant_id,
        ModelType=setting.ModelType,
        Provider=setting.Provider,
        ApiKey=setting.ApiKey,
        ModelName=setting.ModelName,
        ApiBaseUrl=setting.ApiBaseUrl,
        Temperature=setting.Temperature,
        Stream=setting.Stream,
        MaxCompletionTokens=setting.MaxCompletionTokens,
        TopP=setting.TopP,
        FrequencyPenalty=setting.FrequencyPenalty,
        PresencePenalty=setting.PresencePenalty,
        Description=setting.Description,
        AudioDataMode=setting.AudioDataMode,
    )
    db.add(new_setting)
    await db.commit()
    await db.refresh(new_setting)
    if new_setting.Id is None:
        new_setting.Id = uuid.uuid4()
    return new_setting


@router.put("/{setting_id}", response_model=ProviderSettingsResponse)
@optional_tenant
async def update_setting(
    setting_id: UUID4,
    setting: ProviderSettingsCreate,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    existing_setting = await _get_scoped_setting(setting_id, tenant_id, db)
    if existing_setting is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="AI provider setting not found.")

    # Only override TenantId with current tenant if it's not explicitly provided in the request
    # This allows setting TenantId to null (Global) if requested by a superadmin or authorized user
    resolved_tenant_id = setting.TenantId
    
    # If TenantId is not in the set of fields provided (null in JSON), it defaults to None in Pydantic Model.
    # However, if it's None, we should keep the current tenant_id context UNLESS the user is trying to make it Global.
    # To fix the issue where setting it to null (Global) isn't working:
    if resolved_tenant_id is None and tenant_id:
        resolved_tenant_id = tenant_id

    existing_setting.TenantId = resolved_tenant_id
    existing_setting.Key = setting.Key
    existing_setting.ModelType = setting.ModelType
    existing_setting.Provider = setting.Provider
    existing_setting.ApiKey = setting.ApiKey
    existing_setting.ModelName = setting.ModelName
    existing_setting.ApiBaseUrl = setting.ApiBaseUrl
    existing_setting.Temperature = setting.Temperature
    existing_setting.Stream = setting.Stream
    existing_setting.MaxCompletionTokens = setting.MaxCompletionTokens
    existing_setting.TopP = setting.TopP
    existing_setting.FrequencyPenalty = setting.FrequencyPenalty
    existing_setting.PresencePenalty = setting.PresencePenalty
    existing_setting.Description = setting.Description
    existing_setting.AudioDataMode = setting.AudioDataMode

    await db.commit()
    await db.refresh(existing_setting)
    return existing_setting


@router.delete("/{setting_id}", status_code=status.HTTP_204_NO_CONTENT)
@optional_tenant
async def delete_setting(
    setting_id: UUID4,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    existing_setting = await _get_scoped_setting(setting_id, tenant_id, db)
    if existing_setting is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="AI provider setting not found.")

    await db.delete(existing_setting)
    await db.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)
