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
from models import AiSystemPrompt

router = APIRouter()

class SystemPromptCreate(BaseModel):
    Name: str
    PromptText: str
    TenantId: Optional[str] = None
    ResponseFormat: Optional[str] = None

class SystemPromptResponse(SystemPromptCreate):
    model_config = ConfigDict(from_attributes=True)
    Id: UUID4


class PromptScopeFilter(str):
    ALL = "all"
    TENANT = "tenant"
    GLOBAL = "global"


async def _get_scoped_prompt(
    prompt_id: uuid.UUID,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> Optional[AiSystemPrompt]:
    query = select(AiSystemPrompt).where(AiSystemPrompt.Id == prompt_id)
    if tenant_id:
        # If tenant_id is provided, allow both tenant-specific and global prompts
        query = query.where(or_(AiSystemPrompt.TenantId == tenant_id, AiSystemPrompt.TenantId.is_(None)))
    else:
        # If no tenant_id is extracted from headers (superadmin/service), allow ALL prompts (global and any tenant)
        # This fixes 404 when superadmins try to delete tenant-owned prompts
        pass

    result = await db.execute(query)
    return result.scalar_one_or_none()

@router.get("/", response_model=List[SystemPromptResponse])
@optional_tenant
async def get_system_prompts(
    scope: str = PromptScopeFilter.ALL,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    normalized_scope = scope.lower()
    if normalized_scope not in {PromptScopeFilter.ALL, PromptScopeFilter.TENANT, PromptScopeFilter.GLOBAL}:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Invalid scope filter. Allowed values: all, tenant, global.",
        )

    query = select(AiSystemPrompt)

    if normalized_scope == PromptScopeFilter.GLOBAL:
        query = query.where(AiSystemPrompt.TenantId.is_(None))
    elif normalized_scope == PromptScopeFilter.TENANT:
        if tenant_id:
            query = query.where(AiSystemPrompt.TenantId == tenant_id)
        else:
            query = query.where(AiSystemPrompt.TenantId.is_not(None))
    else:
        if tenant_id:
            query = query.where(or_(AiSystemPrompt.TenantId == tenant_id, AiSystemPrompt.TenantId.is_(None)))

    result = await db.execute(query)
    return result.scalars().all()


@router.get("/{prompt_id}", response_model=SystemPromptResponse)
@optional_tenant
async def get_system_prompt(
    prompt_id: UUID4,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    prompt = await _get_scoped_prompt(prompt_id, tenant_id, db)
    if prompt is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="System prompt not found.")

    return prompt

@router.post("/", response_model=SystemPromptResponse)
@optional_tenant
async def create_system_prompt(
    prompt: SystemPromptCreate,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    resolved_tenant_id = prompt.TenantId
    if resolved_tenant_id is None and tenant_id:
        resolved_tenant_id = tenant_id

    new_prompt = AiSystemPrompt(
        TenantId=resolved_tenant_id,
        Name=prompt.Name,
        PromptText=prompt.PromptText,
        ResponseFormat=prompt.ResponseFormat,
    )
    db.add(new_prompt)
    await db.commit()
    await db.refresh(new_prompt)
    if new_prompt.Id is None:
        new_prompt.Id = uuid.uuid4()
    return new_prompt


@router.put("/{prompt_id}", response_model=SystemPromptResponse)
@optional_tenant
async def update_system_prompt(
    prompt_id: UUID4,
    prompt: SystemPromptCreate,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    existing_prompt = await _get_scoped_prompt(prompt_id, tenant_id, db)
    if existing_prompt is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="System prompt not found.")

    resolved_tenant_id = prompt.TenantId
    if resolved_tenant_id is None:
        resolved_tenant_id = tenant_id if tenant_id else existing_prompt.TenantId

    existing_prompt.TenantId = resolved_tenant_id
    existing_prompt.Name = prompt.Name
    existing_prompt.PromptText = prompt.PromptText
    existing_prompt.ResponseFormat = prompt.ResponseFormat

    await db.commit()
    await db.refresh(existing_prompt)
    return existing_prompt


@router.delete("/{prompt_id}", status_code=status.HTTP_204_NO_CONTENT)
@optional_tenant
async def delete_system_prompt(
    prompt_id: UUID4,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_superadmin_or_service)
):
    existing_prompt = await _get_scoped_prompt(prompt_id, tenant_id, db)
    if existing_prompt is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="System prompt not found.")

    await db.delete(existing_prompt)
    await db.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)
