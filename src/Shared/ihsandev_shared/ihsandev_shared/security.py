"""
security.py — JWT verification + SharedSecret service-to-service auth dependency.

Mirrors the .NET ServiceAuthenticationMiddleware + JwtBearerAuthentication pattern.

Headers used for service-to-service (matches .NET):
  X-Service-Secret  — shared secret value (must match ServiceCommunication.SharedSecret)
  X-Service-Name    — calling service name (validated against AllowedServices whitelist)

End-user requests use standard:
  Authorization: Bearer <jwt_token>

Usage:
    from ihsandev_shared.security import make_auth_dependency
    from core.config import settings

    # Create a reusable FastAPI dependency
    get_current_user_or_service = make_auth_dependency(settings.Jwt, settings.ServiceCommunication)

    # In a route
    @router.get("/")
    async def my_route(auth: dict = Depends(get_current_user_or_service)):
        ...
"""
import logging

import jwt
from fastapi import HTTPException, Request, status

from .config import JwtSettings, ServiceCommunicationSettings

logger = logging.getLogger(__name__)


def verify_jwt_token(token: str, secret: str, audience: str, issuer: str) -> dict:
    """
    Decodes and validates a JWT token.
    Raises HTTPException 401 on failure.
    """
    try:
        payload = jwt.decode(
            token,
            key=secret,
            algorithms=["HS256"],
            audience=audience,
            issuer=issuer,
        )
        return payload
    except jwt.ExpiredSignatureError:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Token has expired")
    except jwt.InvalidTokenError:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid token")


def make_auth_dependency(jwt_settings: JwtSettings, service_comm: ServiceCommunicationSettings):
    """
    Factory that returns a FastAPI dependency callable for dual-mode authentication:

      1. Internal service-to-service:  X-Service-Secret + X-Service-Name headers
         → returns {"role": "Service", "service_name": str, "is_internal": True}

      2. End-user JWT bearer token:
         → returns {"role": "User", "payload": dict}

    Equivalent to .NET ServiceAuthenticationMiddleware combined with
    JwtBearerAuthentication configured on every endpoint.
    """

    async def get_current_user_or_service(request: Request) -> dict:
        # --- 1. Service-to-service auth ---
        if service_comm.Enabled:
            secret_header = request.headers.get("X-Service-Secret")
            service_name_header = request.headers.get("X-Service-Name", "")

            if secret_header is not None:
                if secret_header != service_comm.SharedSecret:
                    logger.warning(
                        "Invalid X-Service-Secret from service '%s' at %s",
                        service_name_header,
                        request.url,
                    )
                    raise HTTPException(
                        status_code=status.HTTP_401_UNAUTHORIZED,
                        detail="Invalid service secret.",
                    )

                # Validate against the allowed-services whitelist if configured
                if service_comm.AllowedServices and service_name_header:
                    if service_name_header not in service_comm.AllowedServices:
                        logger.warning(
                            "Service '%s' is not in AllowedServices list.", service_name_header
                        )
                        raise HTTPException(
                            status_code=status.HTTP_403_FORBIDDEN,
                            detail=f"Service '{service_name_header}' is not allowed to call this service.",
                        )

                logger.debug("Internal service request from '%s'", service_name_header)
                return {
                    "role": "Service",
                    "service_name": service_name_header,
                    "is_internal": True,
                }

        # --- 2. End-user JWT auth ---
        auth_header = request.headers.get("Authorization")
        if not auth_header or not auth_header.startswith("Bearer "):
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Not authenticated",
                headers={"WWW-Authenticate": "Bearer"},
            )

        token = auth_header[len("Bearer "):]
        payload = verify_jwt_token(
            token,
            secret=jwt_settings.Secret,
            audience=jwt_settings.Audience,
            issuer=jwt_settings.Issuer,
        )
        return {"role": "User", "payload": payload}

    return get_current_user_or_service
