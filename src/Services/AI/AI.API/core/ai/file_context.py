from typing import Any, List, Optional

from ihsandev_shared.clients import FileManagerServiceClient

from core.config import settings

# Singleton — created once at import time, shared across requests.
file_manager_client = FileManagerServiceClient(
    base_url=settings.FileManagerSettings.BaseUrl,
    shared_secret=settings.ServiceCommunication.SharedSecret,
    service_name=settings.ServiceCommunication.ServiceName,
)


def build_file_context_message(files_metadata: Any) -> Optional[dict[str, str]]:
    """Convert a list of file metadata dicts into a single user message listing the file URLs."""
    if not files_metadata:
        return None

    file_lines: list[str] = []
    for f in files_metadata:
        if not isinstance(f, dict):
            continue
        file_url = f.get("external_url") or f.get("externalUrl") or f.get("url")
        if not file_url:
            continue
        name = f.get("name", "file")
        extension = f.get("extension", "")
        file_lines.append(f"- {name}{extension} → {file_url}")

    if not file_lines:
        return None

    return {
        "role": "user",
        "content": "The following files are attached to this message:\n" + "\n".join(file_lines),
    }


async def inject_file_context_if_present(
    litellm_messages: List[dict[str, str]],
    file_ids: List[int],
    tenant_id: Optional[str],
) -> List[dict[str, str]]:
    """Inject a file-listing message immediately before the last user message when file_ids are provided."""
    if not file_ids:
        return litellm_messages

    files_metadata = await file_manager_client.get_files_by_ids(file_ids, tenant_id)
    file_context = build_file_context_message(files_metadata)
    if not file_context:
        return litellm_messages

    # Insert file context just before the final user message.
    return litellm_messages[:-1] + [file_context] + litellm_messages[-1:]
