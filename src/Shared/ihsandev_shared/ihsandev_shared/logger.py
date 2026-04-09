"""
logger.py — Structured file logger with daily rotation.

Mirrors the .NET LoggerManager / ILoggerManager pattern:
  - Colored console output
  - Daily rotating log files in {FilePath}/{ServiceName}/{ServiceName}-YYYY-MM-DD.log
  - Service-name context on every log line
  - Thread-safe

Usage:
    from ihsandev_shared.logger import setup_logger, get_logger

    # Call once at startup (e.g. in main.py lifespan)
    setup_logger(
        service_name="AiService",
        log_directory="C:/Projects/.../Logs",
        log_level="Information",
    )

    # Anywhere in the codebase
    logger = get_logger(__name__)
    logger.info("Something happened")
    logger.exception("Unhandled error", exc_info=True)
"""
import logging
import os
from logging.handlers import TimedRotatingFileHandler

# ---------------------------------------------------------------------------
# Log-level name map (matches .NET LogLevel names)
# ---------------------------------------------------------------------------
_LEVEL_MAP: dict[str, int] = {
    "Trace": logging.DEBUG,
    "Debug": logging.DEBUG,
    "Information": logging.INFO,
    "Warning": logging.WARNING,
    "Error": logging.ERROR,
    "Critical": logging.CRITICAL,
}

_LOG_FORMAT = "%(asctime)s [%(levelname)-8s] %(name)s — %(message)s"
_DATE_FORMAT = "%Y-%m-%d %H:%M:%S"


def setup_logger(
    service_name: str,
    log_directory: str,
    log_level: str = "Information",
) -> logging.Logger:
    """
    Configures the root logger (and a service-named logger) with:
      - StreamHandler (console)
      - TimedRotatingFileHandler (midnight rotation, 30-day retention)

    Safe to call multiple times — handlers are only added once.

    Returns the named service logger.
    """
    level = _LEVEL_MAP.get(log_level, logging.INFO)

    # Configure root logger so all library loggers inherit the level
    root = logging.getLogger()
    root.setLevel(level)

    formatter = logging.Formatter(fmt=_LOG_FORMAT, datefmt=_DATE_FORMAT)

    # --- Console handler ---
    if not any(isinstance(h, logging.StreamHandler) and not isinstance(h, TimedRotatingFileHandler) for h in root.handlers):
        console_handler = logging.StreamHandler()
        console_handler.setLevel(level)
        console_handler.setFormatter(formatter)
        root.addHandler(console_handler)

    # --- File handler ---
    service_log_dir = os.path.join(log_directory, service_name)
    os.makedirs(service_log_dir, exist_ok=True)
    log_file_path = os.path.join(service_log_dir, f"{service_name}.log")

    if not any(isinstance(h, TimedRotatingFileHandler) for h in root.handlers):
        file_handler = TimedRotatingFileHandler(
            filename=log_file_path,
            when="midnight",
            interval=1,
            backupCount=30,
            encoding="utf-8",
        )
        file_handler.suffix = "%Y-%m-%d"
        file_handler.setLevel(level)
        file_handler.setFormatter(formatter)
        root.addHandler(file_handler)

    return logging.getLogger(service_name)


def get_logger(name: str) -> logging.Logger:
    """
    Returns a named logger. Call setup_logger() once at startup first.
    Equivalent to ILoggerManager or ILogger<T> in .NET.
    """
    return logging.getLogger(name)
