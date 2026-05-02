import os
import subprocess
import sys
from contextlib import asynccontextmanager

import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware

from core.config import settings
from core.database import ensure_database_exists, ensure_schema_exists
from core.exceptions import (
    AppException,
    app_exception_handler,
    global_exception_handler,
    http_exception_handler,
    request_validation_exception_handler,
)
from core.logger import get_logger, init_logging

# ---------------------------------------------------------------------------
# Logging must be initialised before any other module logs anything
# ---------------------------------------------------------------------------
init_logging()
logger = get_logger(__name__)

# Suppress verbose third-party logs
import logging
logging.getLogger("httpcore").setLevel(logging.WARNING)
logging.getLogger("httpx").setLevel(logging.WARNING)
logging.getLogger("watchfiles").setLevel(logging.WARNING)
logging.getLogger("litellm").setLevel(logging.WARNING)
logging.getLogger("LiteLLM").setLevel(logging.WARNING)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Starting %s...", settings.ServiceCommunication.ServiceName)

    # 1. Ensure the target database exists (mirrors .NET UseDefaultDatabaseMigration)
    await ensure_database_exists()

    # 2. Run Alembic migrations (mirrors .NET automatic EF Core migrations)
    logger.info("Running database migrations...")
    try:
        # Resolve alembic binary relative to the current Python executable
        # so it works inside any virtual environment on any OS
        alembic_bin = os.path.join(os.path.dirname(sys.executable), "alembic")
        if sys.platform == "win32":
            alembic_bin += ".exe"
        if not os.path.isfile(alembic_bin):
            alembic_bin = "alembic"  # fall back to PATH

        # Ensure we run in the directory where alembic.ini is located
        cwd = os.path.dirname(os.path.abspath(__file__))
        subprocess.run([alembic_bin, "upgrade", "head"], check=True, cwd=cwd)
        logger.info("Database migrations completed successfully.")
    except Exception:
        logger.exception("Failed to run database migrations.")

    # 3. Ensure tables exist even when no Alembic revisions are present yet
    logger.info("Ensuring database schema exists...")
    await ensure_schema_exists()
    logger.info("Database schema check completed.")

    yield

    logger.info("Shutting down %s.", settings.ServiceCommunication.ServiceName)


# ---------------------------------------------------------------------------
# FastAPI application
# ---------------------------------------------------------------------------
app = FastAPI(
    title="AI.API",
    description="Microservices AI Gateway utilizing LiteLLM",
    version="1.0.0",
    lifespan=lifespan,
)

# --- Exception handlers (mirrors .NET GlobalExceptionHandlingMiddleware) ---
app.add_exception_handler(Exception, global_exception_handler)
app.add_exception_handler(AppException, app_exception_handler)
app.add_exception_handler(HTTPException, http_exception_handler)
app.add_exception_handler(RequestValidationError, request_validation_exception_handler)

# --- CORS (reads AllowedOrigins from appsettings.json, mirrors .NET CORS config) ---
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.Cors.AllowedOrigins or ["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ---------------------------------------------------------------------------
# Routers
# ---------------------------------------------------------------------------
from api.routes import settings as settings_router
from api.routes import system_prompts as prompts_router
from api.routes import chat as chat_router
from api.routes import chat_sessions as chat_sessions_router
from api.routes import chat_messages as chat_messages_router
from api.routes import chat_messages as chat_messages_router
from api.routes import chat_message_files as chat_message_files_router
from api.routes import token_usage_logs as token_usage_logs_router
from api.routes import embedding as embedding_router

app.include_router(settings_router.router, prefix="/api/v1/settings", tags=["Settings"])
app.include_router(prompts_router.router, prefix="/api/v1/prompts", tags=["System Prompts"])
app.include_router(chat_router.router, prefix="/api/v1/chat", tags=["AI Chat"])
app.include_router(chat_sessions_router.router, prefix="/api/v1/chat-sessions", tags=["Chat Sessions"])
app.include_router(chat_messages_router.router, prefix="/api/v1/chat-messages", tags=["Chat Messages"])
app.include_router(chat_messages_router.router, prefix="/api/v1/chat-messages", tags=["Chat Messages"])
app.include_router(chat_message_files_router.router, prefix="/api/v1/chat-message-files", tags=["Chat Message Files"])
app.include_router(token_usage_logs_router.router, prefix="/api/v1/token-usage-logs", tags=["Token Usage Logs"])
app.include_router(embedding_router.router, prefix="/api/v1/embedding", tags=["Embedding"])


@app.get("/health")
async def health_check():
    return {"status": "healthy", "service": settings.ServiceCommunication.ServiceName}


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    url_parts = settings.Urls.replace("http://", "").replace("https://", "").split(":")
    host = url_parts[0]
    port = int(url_parts[1]) if len(url_parts) > 1 else 5008
    uvicorn.run("main:app", host=host, port=port, reload=True)
