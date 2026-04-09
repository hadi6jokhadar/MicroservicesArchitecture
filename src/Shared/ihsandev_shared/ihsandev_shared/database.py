"""
database.py — Shared async PostgreSQL utilities.

Mirrors the .NET EnsureCreated() + Npgsql connection string pattern.

Provides:
  parse_connection_string  — converts .NET Npgsql format → asyncpg URL
  ensure_database_exists   — creates the target DB if it doesn't exist

These are pure utility functions; SQLAlchemy engine/session setup is
done per-service (each service owns its own Base and AsyncSessionFactory).
"""
import logging
import re

import asyncpg

logger = logging.getLogger(__name__)


def parse_connection_string(conn_str: str) -> str:
    """
    Converts a .NET Npgsql-format connection string to an asyncpg-compatible SQLAlchemy URL.

    Input:  "Host=localhost;Port=5432;Database=ai;Username=postgres;Password=s3cr3t;"
    Output: "postgresql+asyncpg://postgres:s3cr3t@localhost:5432/ai"

    Handles passwords that contain '=' by splitting only on the FIRST '=' per segment.
    """
    parts: dict[str, str] = {}
    for segment in conn_str.split(";"):
        segment = segment.strip()
        if not segment:
            continue
        idx = segment.find("=")
        if idx == -1:
            continue
        key = segment[:idx].strip()
        value = segment[idx + 1:].strip()
        parts[key] = value

    host = parts.get("Host", "localhost")
    port = parts.get("Port", "5432")
    db = parts.get("Database", "")
    user = parts.get("Username", "postgres")
    password = parts.get("Password", "")

    return f"postgresql+asyncpg://{user}:{password}@{host}:{port}/{db}"


async def ensure_database_exists(async_database_url: str) -> None:
    """
    Connects to the 'postgres' maintenance database and creates the target
    database if it does not already exist.

    Equivalent to .NET EnsureCreated() / UseDefaultDatabaseMigration startup logic.

    Args:
        async_database_url: Full asyncpg URL, e.g. postgresql+asyncpg://user:pass@host/dbname
    """
    db_name = async_database_url.rstrip("/").split("/")[-1]

    # Guard against SQL injection — database names must be alphanumeric + underscores
    if not re.match(r"^[a-zA-Z0-9_]+$", db_name):
        raise ValueError(f"Invalid database name: '{db_name}'. Only alphanumeric and underscore characters are allowed.")

    sys_url = (
        async_database_url
        .replace(f"/{db_name}", "/postgres")
        .replace("postgresql+asyncpg://", "postgresql://")
    )

    try:
        conn = await asyncpg.connect(sys_url)
        try:
            exists = await conn.fetchval(
                "SELECT 1 FROM pg_database WHERE datname = $1", db_name
            )
            if not exists:
                logger.info("Database '%s' does not exist. Creating...", db_name)
                # db_name is safely validated above; quoting prevents reserved-word conflicts
                await conn.execute(f'CREATE DATABASE "{db_name}"')
                logger.info("Database '%s' created successfully.", db_name)
            else:
                logger.debug("Database '%s' already exists.", db_name)
        finally:
            await conn.close()
    except Exception:
        logger.exception("Could not check/create database '%s'", db_name)
        raise
