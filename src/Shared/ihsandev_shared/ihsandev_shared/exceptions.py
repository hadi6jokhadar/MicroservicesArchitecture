"""Shared ProblemDetails exception handlers for FastAPI.

This module mirrors the .NET shared exception pipeline shape used by other services:
- status
- title
- detail
- instance
- traceId
- errors (for validation failures)
"""
import logging
from http import HTTPStatus
from typing import Any
from uuid import uuid4

from fastapi import HTTPException, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

logger = logging.getLogger(__name__)


class AppException(Exception):
    """
    Application-level exception that maps to a specific HTTP status code.
    Equivalent to .NET AppException / GuardExtensions throw pattern.

    Usage:
        raise AppException("Tenant not found.", status_code=404)
        raise AppException("Validation failed.", status_code=422)
    """

    def __init__(self, message: str, status_code: int = 400, title: str | None = None):
        self.message = message
        self.status_code = status_code
        self.title = title or _status_title(status_code)
        super().__init__(message)


def _status_title(status_code: int) -> str:
    try:
        return HTTPStatus(status_code).phrase
    except ValueError:
        return "Error"


def _to_camel_case(value: str) -> str:
    if not value:
        return value
    return value[0].lower() + value[1:]


def _get_trace_id(request: Request) -> str:
    # Prefer propagated ids from upstream, then fallback to runtime-generated id.
    return (
        request.headers.get("x-trace-id")
        or request.headers.get("x-request-id")
        or getattr(request.state, "trace_id", None)
        or request.scope.get("trace_id")
        or str(uuid4())
    )


def _problem_details(
    request: Request,
    *,
    status_code: int,
    title: str,
    detail: str,
    errors: dict[str, list[str]] | None = None,
) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "status": status_code,
        "title": title,
        "detail": detail,
        "instance": request.url.path,
        "traceId": _get_trace_id(request),
    }
    if errors:
        payload["errors"] = errors
    return payload


def _extract_validation_errors(exc: RequestValidationError) -> dict[str, list[str]]:
    errors: dict[str, list[str]] = {}

    for err in exc.errors():
        loc = list(err.get("loc", []))
        msg = str(err.get("msg", "Invalid input."))

        filtered = [str(p) for p in loc if p not in {"body", "query", "path", "header", "cookie"}]
        if not filtered:
            key = "request"
        else:
            normalized: list[str] = []
            for part in filtered:
                if part.isdigit() and normalized:
                    normalized[-1] = f"{normalized[-1]}[{part}]"
                else:
                    normalized.append(_to_camel_case(part))
            key = ".".join(normalized)

        errors.setdefault(key, []).append(msg)

    return errors


async def app_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    """Handles AppException in .NET-like ProblemDetails format."""
    # Narrow the exception type to AppException to access its properties
    if not isinstance(exc, AppException):
        # This handler should only be registered for AppException,
        # but the signature requires Exception for type safety in FastAPI.
        return await global_exception_handler(request, exc)

    logger.warning("AppException on %s: %s", request.url, exc.message)
    return JSONResponse(
        status_code=exc.status_code,
        content=_problem_details(
            request,
            status_code=exc.status_code,
            title=exc.title,
            detail=exc.message,
        ),
        media_type="application/problem+json",
    )


async def request_validation_exception_handler(
    request: Request, exc: Exception
) -> JSONResponse:
    """Converts FastAPI/Pydantic validation errors to shared ProblemDetails format."""
    if not isinstance(exc, RequestValidationError):
        return await global_exception_handler(request, exc)

    errors = _extract_validation_errors(exc)
    logger.warning("Validation failed on %s: %s", request.url, errors)

    return JSONResponse(
        status_code=status.HTTP_400_BAD_REQUEST,
        content=_problem_details(
            request,
            status_code=status.HTTP_400_BAD_REQUEST,
            title="Bad Request",
            detail="One or more validation errors occurred",
            errors=errors,
        ),
        media_type="application/problem+json",
    )


async def http_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    """Formats HTTPException using the shared ProblemDetails shape."""
    if not isinstance(exc, HTTPException):
        return await global_exception_handler(request, exc)

    detail = exc.detail if isinstance(exc.detail, str) else "Request failed"
    status_code = exc.status_code or status.HTTP_500_INTERNAL_SERVER_ERROR

    return JSONResponse(
        status_code=status_code,
        content=_problem_details(
            request,
            status_code=status_code,
            title=_status_title(status_code),
            detail=detail,
        ),
        media_type="application/problem+json",
        headers=exc.headers,
    )


async def global_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    """Catches unhandled exceptions and returns safe shared ProblemDetails."""
    logger.exception("Unhandled exception on %s", request.url)
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content=_problem_details(
            request,
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            title="Internal Server Error",
            detail="An unexpected error occurred. Please try again later.",
        ),
        media_type="application/problem+json",
    )
