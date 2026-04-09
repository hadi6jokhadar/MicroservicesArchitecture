"""
core/exceptions.py — Re-exports shared exception handlers.

All exception-handling logic lives in ihsandev_shared.exceptions.
This module exists only as the service-local import point.
"""
from ihsandev_shared.exceptions import (
    AppException,
    app_exception_handler,
    global_exception_handler,
    http_exception_handler,
    request_validation_exception_handler,
)

__all__ = [
    "AppException",
    "app_exception_handler",
    "global_exception_handler",
    "http_exception_handler",
    "request_validation_exception_handler",
]
