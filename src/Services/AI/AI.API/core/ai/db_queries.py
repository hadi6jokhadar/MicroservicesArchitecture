from typing import Optional

from fastapi import HTTPException, status
from sqlalchemy import or_
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select

from models import AiProviderSettings, AiSystemPrompt


async def get_settings_by_key(
    key: str,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> AiProviderSettings:
    """Lookup AiProviderSettings by Key.

    With tenant context: prefer tenant row, then fall back to global (TenantId IS NULL).
    Without tenant context: resolve by key regardless of TenantId.
    """
    if tenant_id:
        stmt = (
            select(AiProviderSettings)
            .where(
                AiProviderSettings.Key == key,
                or_(
                    AiProviderSettings.TenantId == tenant_id,
                    AiProviderSettings.TenantId.is_(None),
                ),
            )
            .order_by(AiProviderSettings.TenantId.is_(None))  # tenant-specific first
        )
    else:
        stmt = select(AiProviderSettings).where(AiProviderSettings.Key == key)

    result = await db.execute(stmt)
    setting = result.scalars().first()
    if not setting:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"AI provider setting '{key}' not found.",
        )
    return setting


async def get_system_prompt_by_key(
    name: str,
    tenant_id: Optional[str],
    db: AsyncSession,
) -> Optional[AiSystemPrompt]:
    """Lookup AiSystemPrompt by Name, scoped to the caller's tenant or global.

    With tenant context: prefer tenant row, then fall back to global.
    Without tenant context: global-only lookup.
    """
    if tenant_id:
        stmt = (
            select(AiSystemPrompt)
            .where(
                AiSystemPrompt.Name == name,
                or_(
                    AiSystemPrompt.TenantId == tenant_id,
                    AiSystemPrompt.TenantId.is_(None),
                ),
            )
            .order_by(AiSystemPrompt.TenantId.is_(None))  # tenant-specific first
        )
    else:
        stmt = select(AiSystemPrompt).where(
            AiSystemPrompt.Name == name,
            AiSystemPrompt.TenantId.is_(None),
        )

    result = await db.execute(stmt)
    return result.scalars().first()
