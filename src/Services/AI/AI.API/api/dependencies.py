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


def _extract_roles(payload: dict) -> set[str]:
    roles: set[str] = set()

    raw_roles = payload.get("roles")
    if isinstance(raw_roles, list):
        roles.update(str(role) for role in raw_roles if role)
    elif isinstance(raw_roles, str) and raw_roles:
        roles.add(raw_roles)

    raw_role = payload.get("role")
    if isinstance(raw_role, list):
        roles.update(str(role) for role in raw_role if role)
    elif isinstance(raw_role, str) and raw_role:
        roles.add(raw_role)

    claim_role = payload.get("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
    if isinstance(claim_role, list):
        roles.update(str(role) for role in claim_role if role)
    elif isinstance(claim_role, str) and claim_role:
        roles.add(claim_role)

    return roles


async def require_superadmin_or_service(user_info: dict = Depends(require_auth)):
    if user_info.get("role") == "Service":
        return user_info

    payload = user_info.get("payload") or {}
    roles = _extract_roles(payload)
    if "SuperAdmin" not in roles:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="SuperAdmin role is required for this endpoint.",
        )

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
