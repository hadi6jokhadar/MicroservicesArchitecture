"""
clients/file_manager.py — Typed client for the .NET FileManager service.

Extends ihsandev_shared.clients.BaseServiceClient which automatically injects
the correct X-Service-Secret + X-Service-Name headers used by .NET's
ServiceAuthenticationMiddleware.
"""
from typing import Any, List
from pydantic import UUID4

from ihsandev_shared.clients import BaseServiceClient
from core.config import settings
from core.logger import get_logger

logger = get_logger(__name__)


class FileManagerClient(BaseServiceClient):
    """
    Typed async HTTP client for FileManager.API inter-service calls.
    Mirrors the .NET IFileManagerServiceClient pattern.
    """

    def __init__(self):
        super().__init__(
            base_url=settings.FileManagerSettings.BaseUrl,
            shared_secret=settings.ServiceCommunication.SharedSecret,
            service_name=settings.ServiceCommunication.ServiceName,
        )

    async def get_files_metadata(
        self, file_ids: List[UUID4], tenant_id: str
    ) -> List[dict[str, Any]]:
        """
        Fetches metadata and download URLs for a list of file IDs from FileManager.API.
        Calls POST /api/v1/files/bulk with X-Service-Secret authentication.
        """
        if not file_ids:
            return []

        str_ids = [str(fid) for fid in file_ids]
        try:
            return await self.post(
                "/api/v1/files/bulk",
                body={"fileIds": str_ids},
                tenant_id=tenant_id,
            )
        except Exception:
            logger.exception(
                "Failed to fetch file metadata for %d file(s) from FileManager.", len(str_ids)
            )
            return []


# Singleton instance — registered on app.state in main.py (mirrors .NET DI singleton)
file_manager_client = FileManagerClient()
