from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from pydantic import BaseModel, UUID4
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
    Id: UUID4

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
        result = await db.execute(select(AiSystemPrompt).where(AiSystemPrompt.TenantId == None))
    
    return result.scalars().all()

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
