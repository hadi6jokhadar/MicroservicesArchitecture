import uuid
from typing import Optional

from fastapi import HTTPException, status
from pydantic import UUID4
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select

from models import AiChatSession

GLOBAL_CHAT_TENANT_ID = "global"


async def resolve_or_create_session(
    requested_session_id: Optional[UUID4],
    tenant_id: Optional[str],
    user_id: Optional[int],
    db: AsyncSession,
) -> uuid.UUID:
    """Return the UUID of an existing chat session or create a new one.

    When *requested_session_id* is provided the session must already exist for
    the resolved tenant; a 404 is raised otherwise.  When it is omitted a new
    session is created and its auto-generated id is returned.
    """
    session_tenant_id = tenant_id or GLOBAL_CHAT_TENANT_ID

    if requested_session_id:
        stmt = select(AiChatSession).where(
            AiChatSession.Id == requested_session_id,
            AiChatSession.TenantId == session_tenant_id,
        )
        result = await db.execute(stmt)
        session = result.scalar_one_or_none()
        if session is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Chat session not found.",
            )
        return session.Id  # type: ignore[return-value]

    new_session = AiChatSession(
        TenantId=session_tenant_id,
        UserId=user_id or 0,
    )
    db.add(new_session)
    await db.flush()  # populate Id without committing
    if new_session.Id is None:
        new_session.Id = uuid.uuid4()
    await db.commit()
    return new_session.Id  # type: ignore[return-value]
