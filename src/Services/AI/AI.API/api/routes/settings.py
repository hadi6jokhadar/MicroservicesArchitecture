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
from models import AiProviderSettings, ModelTypeEnum

router = APIRouter()

class ProviderSettingsCreate(BaseModel):
    ModelType: ModelTypeEnum
    Provider: str
    ApiKey: str
    ModelName: str
    TenantId: Optional[str] = None

class ProviderSettingsResponse(ProviderSettingsCreate):
    model_config = ConfigDict(from_attributes=True)
    Id: UUID4


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
        query = query.where(AiProviderSettings.TenantId == tenant_id)
    else:
        query = query.where(AiProviderSettings.TenantId.is_(None))

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
        TenantId=resolved_tenant_id,
        ModelType=setting.ModelType,
        Provider=setting.Provider,
        ApiKey=setting.ApiKey,
        ModelName=setting.ModelName,
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

    resolved_tenant_id = setting.TenantId
    if resolved_tenant_id is None:
        resolved_tenant_id = tenant_id if tenant_id else existing_setting.TenantId

    existing_setting.TenantId = resolved_tenant_id
    existing_setting.ModelType = setting.ModelType
    existing_setting.Provider = setting.Provider
    existing_setting.ApiKey = setting.ApiKey
    existing_setting.ModelName = setting.ModelName

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
