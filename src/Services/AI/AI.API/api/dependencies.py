from typing import Optional

from fastapi import Depends, HTTPException, Request, status
from sqlalchemy.ext.asyncio import AsyncSession

from core.database import get_db
from core.security import get_current_user_or_service


async def get_db_session(session: AsyncSession = Depends(get_db)):
    """Provides a database session for routes."""
    return session


async def require_auth(user_info: dict = Depends(get_current_user_or_service)):
    """
    Ensures the request is authenticated either via X-Service-Secret or JWT Bearer.
    Returns the user/service payload dict.
    """
    return user_info


def _allows_missing_tenant(request: Request) -> bool:
    """Checks route metadata flags that mirror .NET OptionalTenant/BypassTenant attributes."""
    endpoint = request.scope.get("endpoint")
    if endpoint is None:
        return False

    return bool(
        getattr(endpoint, "_optional_tenant", False)
        or getattr(endpoint, "_bypass_tenant", False)
    )


async def get_tenant_id(request: Request, user_info: dict = Depends(require_auth)) -> Optional[str]:
    """
    Resolves the tenant ID from:
      1. x-tenant-id header (highest priority)
      2. tenantId claim inside the JWT payload (for end-user requests)

    Raises 400 if a user-role request has no resolvable tenant.
    Internal service calls (role=Service) may omit the tenant header
    when operating on global/admin data.
    """
    tenant_header = request.headers.get("x-tenant-id")
    if tenant_header:
        return tenant_header

    if user_info.get("role") == "User":
        tenant_id = user_info.get("payload", {}).get("tenantId")
        if not tenant_id:
            if _allows_missing_tenant(request):
                return None

            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Tenant context is required. Provide the 'x-tenant-id' header or ensure your JWT contains a 'tenantId' claim.",
            )
        return tenant_id

    # Internal service calls without a tenant header operate in global scope
    return None
