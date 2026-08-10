import os

from sqlalchemy import text
from sqlalchemy.ext.asyncio import create_async_engine, async_sessionmaker, AsyncSession
from sqlalchemy.orm import declarative_base

from ihsandev_shared.database import (
    parse_connection_string,
    ensure_database_exists as _ensure_db,
)
from core.config import settings

# Build the async URL using the shared, robust parser
ASYNC_DATABASE_URL = parse_connection_string(settings.DatabaseSettings.ConnectionString)

engine = create_async_engine(
    ASYNC_DATABASE_URL,
    echo=False,
    future=True,
)

AsyncSessionFactory = async_sessionmaker(
    engine,
    autoflush=False,
    expire_on_commit=False,
    class_=AsyncSession,
)

Base = declarative_base()


async def get_db():
    async with AsyncSessionFactory() as session:
        yield session


async def ensure_database_exists() -> None:
    """Creates the AI database if it does not already exist."""
    await _ensure_db(ASYNC_DATABASE_URL)


async def ensure_schema_exists() -> None:
    """Creates missing tables from SQLAlchemy metadata (idempotent)."""
    # Import models here so Base.metadata contains all mapped tables.
    import models  # noqa: F401

    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)


def _alembic_head_revision() -> str:
    """Resolves the head revision from the alembic/versions/ scripts on disk."""
    from alembic.config import Config as AlembicConfig
    from alembic.script import ScriptDirectory

    ini_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "alembic.ini")
    script_dir = ScriptDirectory.from_config(AlembicConfig(ini_path))
    return script_dir.get_current_head()


async def get_schema_health() -> tuple[bool, str]:
    """
    Reachability + migration-drift check for the health endpoint. The lifespan handler's
    `alembic upgrade head` call only logs on failure and never stops startup (see main.py),
    so this is what actually surfaces a failed/skipped migration as unhealthy instead of the
    service silently serving requests against a stale schema.
    """
    try:
        async with engine.connect() as conn:
            result = await conn.execute(text("SELECT version_num FROM alembic_version"))
            current_revision = result.scalar()
    except Exception as ex:
        return False, f"database unreachable: {ex}"

    if current_revision is None:
        return False, "no migration history found (alembic_version is empty) — schema may be out of sync"

    try:
        head_revision = _alembic_head_revision()
    except Exception as ex:
        # Can't resolve the expected head (e.g. alembic.ini missing in this deployment) —
        # don't fail the whole health check over a packaging issue, just say so.
        return True, f"connected (revision {current_revision}); could not verify against head: {ex}"

    if current_revision != head_revision:
        return False, f"schema out of sync — database is at revision {current_revision}, code expects {head_revision}"

    return True, f"connected, schema up to date (revision {current_revision})"
