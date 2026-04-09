"""
ihsandev_shared — Python equivalents of the .NET IhsanDev.Shared.* libraries.

Provides:
  config     → appsettings.json loader + base Pydantic settings models
  logger     → file logger with daily rotation (mirrors LoggerManager)
  security   → JWT verify + SharedSecret service-to-service auth (mirrors ServiceAuthenticationMiddleware)
  exceptions → RFC 7807 ProblemDetails handlers (mirrors GlobalExceptionHandlingMiddleware)
  database   → parse_connection_string + ensure_database_exists utilities
  clients    → BaseServiceClient for service-to-service HTTP calls
"""
from .config import (
    load_json_settings,
    BaseAppSettings,
    DatabaseSettings,
    JwtSettings,
    ServiceCommunicationSettings,
    CorsSettings,
    LoggingLevelSettings,
    LoggingSettings,
)
from .logger import setup_logger, get_logger
from .security import verify_jwt_token, make_auth_dependency
from .exceptions import global_exception_handler, app_exception_handler, AppException
from .database import parse_connection_string, ensure_database_exists

__all__ = [
    "load_json_settings",
    "BaseAppSettings",
    "DatabaseSettings",
    "JwtSettings",
    "ServiceCommunicationSettings",
    "CorsSettings",
    "LoggingLevelSettings",
    "LoggingSettings",
    "setup_logger",
    "get_logger",
    "verify_jwt_token",
    "make_auth_dependency",
    "global_exception_handler",
    "app_exception_handler",
    "AppException",
    "parse_connection_string",
    "ensure_database_exists",
]
