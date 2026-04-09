"""
clients/base_client.py — Base async HTTP client for service-to-service communication.

Mirrors the .NET INotificationServiceClient / IFileManagerServiceClient pattern.
Automatically injects the correct headers for SharedSecret authentication:
  X-Service-Secret  — matches ServiceCommunication.SharedSecret in appsettings.json
  X-Service-Name    — identifies the calling service

Usage:
    from ihsandev_shared.clients import BaseServiceClient
    from core.config import settings

    class FileManagerClient(BaseServiceClient):
        def __init__(self):
            super().__init__(
                base_url=settings.FileManagerSettings.BaseUrl,
                shared_secret=settings.ServiceCommunication.SharedSecret,
                service_name=settings.ServiceCommunication.ServiceName,
            )

        async def get_file_metadata(self, file_id: str, tenant_id: str) -> dict:
            return await self.get(f"/api/v1/files/{file_id}", tenant_id=tenant_id)
"""
import logging
from typing import Any

import httpx

logger = logging.getLogger(__name__)


class BaseServiceClient:
    """
    Async HTTP client base class for internal service-to-service calls.

    Subclass this in each service for every downstream .NET service you call.
    Equivalent to .NET's typed HttpClient registrations with DelegatingHandler
    that injects X-Service-Secret + X-Service-Name.
    """

    def __init__(self, base_url: str, shared_secret: str, service_name: str):
        self._base_url = base_url.rstrip("/")
        self._base_headers = {
            "X-Service-Secret": shared_secret,
            "X-Service-Name": service_name,
            "Accept": "application/json",
            "Content-Type": "application/json",
        }

    def _build_headers(self, tenant_id: str | None = None) -> dict:
        headers = self._base_headers.copy()
        if tenant_id:
            headers["x-tenant-id"] = tenant_id
        return headers

    async def get(
        self,
        path: str,
        tenant_id: str | None = None,
        **kwargs: Any,
    ) -> Any:
        """Performs a GET request to the downstream service."""
        async with httpx.AsyncClient() as client:
            try:
                response = await client.get(
                    f"{self._base_url}{path}",
                    headers=self._build_headers(tenant_id),
                    **kwargs,
                )
                response.raise_for_status()
                return response.json()
            except httpx.HTTPStatusError as exc:
                logger.error(
                    "GET %s%s failed: %s %s",
                    self._base_url, path, exc.response.status_code, exc.response.text,
                )
                raise
            except httpx.RequestError as exc:
                logger.error("Network error calling %s%s: %s", self._base_url, path, exc)
                raise

    async def post(
        self,
        path: str,
        body: Any,
        tenant_id: str | None = None,
        **kwargs: Any,
    ) -> Any:
        """Performs a POST request with a JSON body to the downstream service."""
        async with httpx.AsyncClient() as client:
            try:
                response = await client.post(
                    f"{self._base_url}{path}",
                    json=body,
                    headers=self._build_headers(tenant_id),
                    **kwargs,
                )
                response.raise_for_status()
                return response.json()
            except httpx.HTTPStatusError as exc:
                logger.error(
                    "POST %s%s failed: %s %s",
                    self._base_url, path, exc.response.status_code, exc.response.text,
                )
                raise
            except httpx.RequestError as exc:
                logger.error("Network error calling %s%s: %s", self._base_url, path, exc)
                raise

    async def put(
        self,
        path: str,
        body: Any,
        tenant_id: str | None = None,
        **kwargs: Any,
    ) -> Any:
        """Performs a PUT request with a JSON body to the downstream service."""
        async with httpx.AsyncClient() as client:
            try:
                response = await client.put(
                    f"{self._base_url}{path}",
                    json=body,
                    headers=self._build_headers(tenant_id),
                    **kwargs,
                )
                response.raise_for_status()
                return response.json()
            except httpx.HTTPStatusError as exc:
                logger.error(
                    "PUT %s%s failed: %s %s",
                    self._base_url, path, exc.response.status_code, exc.response.text,
                )
                raise
            except httpx.RequestError as exc:
                logger.error("Network error calling %s%s: %s", self._base_url, path, exc)
                raise

    async def delete(
        self,
        path: str,
        tenant_id: str | None = None,
        **kwargs: Any,
    ) -> Any:
        """Performs a DELETE request to the downstream service."""
        async with httpx.AsyncClient() as client:
            try:
                response = await client.delete(
                    f"{self._base_url}{path}",
                    headers=self._build_headers(tenant_id),
                    **kwargs,
                )
                response.raise_for_status()
                return response.json()
            except httpx.HTTPStatusError as exc:
                logger.error(
                    "DELETE %s%s failed: %s %s",
                    self._base_url, path, exc.response.status_code, exc.response.text,
                )
                raise
            except httpx.RequestError as exc:
                logger.error("Network error calling %s%s: %s", self._base_url, path, exc)
                raise
