from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, UUID4
from typing import List, Optional
import uuid

from core.database import get_db
from api.dependencies import require_auth, get_tenant_id
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
    Id: UUID4

@router.get("/", response_model=List[ProviderSettingsResponse])
@optional_tenant
async def get_settings(
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
):
    if tenant_id:
        result = await db.execute(select(AiProviderSettings).where(AiProviderSettings.TenantId == tenant_id))
    else:
        # Get Global settings
        result = await db.execute(select(AiProviderSettings).where(AiProviderSettings.TenantId == None))
    
    return result.scalars().all()

@router.post("/", response_model=ProviderSettingsResponse)
@optional_tenant
async def create_setting(
    setting: ProviderSettingsCreate,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
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
        ModelName=setting.ModelName
    )
    db.add(new_setting)
    await db.commit()
    await db.refresh(new_setting)
    if new_setting.Id is None:
        new_setting.Id = uuid.uuid4()
    return new_setting
