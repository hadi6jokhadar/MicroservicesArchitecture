"""
core/security.py — Thin wrapper that wires up ihsandev_shared auth using this service's settings.

Exposes a single `get_current_user_or_service` FastAPI dependency ready for injection
in api/dependencies.py and route handlers.
"""
from ihsandev_shared.security import make_auth_dependency
from core.config import settings

# Build the reusable dependency using the service's own JWT + ServiceCommunication config
get_current_user_or_service = make_auth_dependency(
    jwt_settings=settings.Jwt,
    service_comm=settings.ServiceCommunication,
)
