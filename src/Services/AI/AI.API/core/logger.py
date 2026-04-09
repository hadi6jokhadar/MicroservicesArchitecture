"""
core/logger.py — Service-scoped logger for the AI service.

Thin wrapper around ihsandev_shared.logger that reads this service's
Logging config from appsettings.json and names loggers under "AiService.*".

Usage:
    from core.logger import get_logger
    logger = get_logger(__name__)
    logger.info("Chat stream started for tenant %s", tenant_id)
"""
from ihsandev_shared.logger import setup_logger as _setup, get_logger as _get

from core.config import settings


def init_logging() -> None:
    """
    Call once at application startup (in main.py lifespan).
    Configures console + daily-rotating file handlers for the whole process.
    """
    _setup(
        service_name=settings.ServiceCommunication.ServiceName,
        log_directory=settings.Logging.FilePath,
        log_level=settings.Logging.LogLevel.Default,
    )


def get_logger(name: str):
    """
    Returns a named logger.  Equivalent to ILogger<T> injection in .NET.

    Example:
        logger = get_logger(__name__)  # → logging.getLogger("ai_service.api.routes.chat")
    """
    return _get(name)
