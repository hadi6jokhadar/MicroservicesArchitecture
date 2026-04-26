"""
clients/file_manager_client.py — Typed client for the .NET FileManager service.

Mirrors the .NET IFileManagerServiceClient pattern.
Automatically injects X-Service-Secret + X-Service-Name headers via BaseServiceClient.

Endpoints called:
  GET  /api/filemanager/internal/files/{fileId}        → get_file_by_id
  GET  /api/filemanager/internal/files/batch           → get_files_by_ids
  PATCH /api/filemanager/internal/files/{fileId}/temp-status → change_temp_status
"""
from typing import Any, Dict, List
from urllib.parse import urlencode

from .base_client import BaseServiceClient

import logging

logger = logging.getLogger(__name__)


class FileManagerServiceClient(BaseServiceClient):
    """
    Typed async HTTP client for FileManager.API inter-service calls.
    Mirrors the .NET IFileManagerServiceClient interface.

    Usage:
        from ihsandev_shared.clients import FileManagerServiceClient

        client = FileManagerServiceClient(
            base_url=settings.FileManagerSettings.BaseUrl,
            shared_secret=settings.ServiceCommunication.SharedSecret,
            service_name=settings.ServiceCommunication.ServiceName,
        )
    """

    async def get_file_by_id(
        self,
        file_id: int,
        tenant_id: str | None = None,
    ) -> Dict[str, Any] | None:
        """
        Fetches a single file's metadata by ID from FileManager.API.
        Mirrors .NET GetFileByIdAsync.

        Returns the file dict or None if not found / request failed.
        """
        path = f"/api/filemanager/internal/files/{file_id}"
        if tenant_id:
            path += f"?tenantId={tenant_id}"

        try:
            return await self.get(path, tenant_id=tenant_id)
        except Exception:
            logger.exception("Failed to fetch file %d from FileManager.", file_id)
            return None

    async def get_files_by_ids(
        self,
        file_ids: List[int],
        tenant_id: str | None = None,
    ) -> List[Dict[str, Any]]:
        """
        Fetches metadata for a batch of file IDs from FileManager.API.
        Mirrors .NET GetFilesByIdsAsync.

        Returns a list of file dicts. Returns empty list on failure.
        """
        if not file_ids:
            return []

        # Build repeated ?fileIds=1&fileIds=2 params (httpx accepts list of tuples)
        params: List[tuple[str, Any]] = [("fileIds", fid) for fid in file_ids]
        if tenant_id:
            params.append(("tenantId", tenant_id))

        try:
            result = await self.get(
                "/api/filemanager/internal/files/batch",
                tenant_id=tenant_id,
                params=params,
            )
            return result if isinstance(result, list) else []
        except Exception:
            logger.exception(
                "Failed to fetch batch of %d file(s) from FileManager.", len(file_ids)
            )
            return []

    async def change_temp_status(
        self,
        file_id: int,
        temp: bool,
        tenant_id: str | None = None,
    ) -> bool:
        """
        Changes the temporary status of a file in FileManager.API.
        Mirrors .NET ChangeTempStatusAsync.

        Returns True on success, False on failure.
        """
        path = f"/api/filemanager/internal/files/{file_id}/temp-status?temp={str(temp).lower()}"
        if tenant_id:
            path += f"&tenantId={tenant_id}"

        try:
            await self.patch(path, tenant_id=tenant_id)
            return True
        except Exception:
            logger.exception(
                "Failed to change temp status for file %d in FileManager.", file_id
            )
            return False
