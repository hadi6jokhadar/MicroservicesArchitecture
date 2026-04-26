import pytest
from fastapi import HTTPException
from starlette.requests import Request

from api.attributes import optional_tenant
from api.dependencies import get_tenant_id


def _build_request(endpoint):
    scope = {
        "type": "http",
        "method": "GET",
        "path": "/api/v1/chat/stream",
        "headers": [],
        "endpoint": endpoint,
    }
    return Request(scope)


@optional_tenant
async def _optional_tenant_endpoint():
    return None


async def _required_tenant_endpoint():
    return None


@pytest.mark.asyncio
async def test_get_tenant_id_allows_missing_for_optional_tenant_endpoint():
    request = _build_request(_optional_tenant_endpoint)
    user_info = {"role": "User", "payload": {"nameid": "1001"}}

    tenant_id = await get_tenant_id(request=request, user_info=user_info)

    assert tenant_id is None


@pytest.mark.asyncio
async def test_get_tenant_id_rejects_missing_for_required_tenant_endpoint():
    request = _build_request(_required_tenant_endpoint)
    user_info = {"role": "User", "payload": {"nameid": "1001"}}

    with pytest.raises(HTTPException) as ex:
        await get_tenant_id(request=request, user_info=user_info)

    assert ex.value.status_code == 400
    assert "Tenant context is required" in ex.value.detail
