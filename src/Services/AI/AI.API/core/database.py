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
