from fastapi import APIRouter, Depends, HTTPException, Response, status
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, ConfigDict, UUID4
from typing import List, Optional
import uuid

from core.database import get_db
from api.dependencies import require_auth, get_tenant_id
from api.attributes import optional_tenant
from models import AiSystemPrompt

router = APIRouter()

class SystemPromptCreate(BaseModel):
    Name: str
    PromptText: str
    TenantId: Optional[str] = None

class SystemPromptResponse(SystemPromptCreate):
    model_config = ConfigDict(from_attributes=True)
    Id: UUID4


async def _get_scoped_prompt(
    prompt_id: uuid.UUID,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> Optional[AiSystemPrompt]:
    query = select(AiSystemPrompt).where(AiSystemPrompt.Id == prompt_id)
    if tenant_id:
        query = query.where(AiSystemPrompt.TenantId == tenant_id)
    else:
        query = query.where(AiSystemPrompt.TenantId.is_(None))

    result = await db.execute(query)
    return result.scalar_one_or_none()

@router.get("/", response_model=List[SystemPromptResponse])
@optional_tenant
async def get_system_prompts(
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
):
    if tenant_id:
        result = await db.execute(select(AiSystemPrompt).where(AiSystemPrompt.TenantId == tenant_id))
    else:
        # Get Global generic prompts
        result = await db.execute(select(AiSystemPrompt).where(AiSystemPrompt.TenantId.is_(None)))
    
    return result.scalars().all()


@router.get("/{prompt_id}", response_model=SystemPromptResponse)
@optional_tenant
async def get_system_prompt(
    prompt_id: UUID4,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
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
    auth: dict = Depends(require_auth)
):
    resolved_tenant_id = prompt.TenantId
    if resolved_tenant_id is None and tenant_id:
        resolved_tenant_id = tenant_id

    new_prompt = AiSystemPrompt(
        TenantId=resolved_tenant_id,
        Name=prompt.Name,
        PromptText=prompt.PromptText
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
    auth: dict = Depends(require_auth)
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

    await db.commit()
    await db.refresh(existing_prompt)
    return existing_prompt


@router.delete("/{prompt_id}", status_code=status.HTTP_204_NO_CONTENT)
@optional_tenant
async def delete_system_prompt(
    prompt_id: UUID4,
    tenant_id: Optional[str] = Depends(get_tenant_id),
    db: AsyncSession = Depends(get_db),
    auth: dict = Depends(require_auth)
):
    existing_prompt = await _get_scoped_prompt(prompt_id, tenant_id, db)
    if existing_prompt is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="System prompt not found.")

    await db.delete(existing_prompt)
    await db.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)
