"""
config.py — appsettings.json loader + base Pydantic settings models.

Mirrors the .NET pattern of reading appsettings.json → appsettings.{env}.json
with environment-specific overrides (ASPNETCORE_ENVIRONMENT).

Usage in a service:
    from ihsandev_shared.config import BaseAppSettings, load_json_settings
    from pydantic import BaseModel
    import os

    class MyServiceSettings(BaseModel):
        BaseUrl: str

    class MyAppSettings(BaseAppSettings):
        MyService: MyServiceSettings

    _base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    _data = load_json_settings(_base_dir)
    settings = MyAppSettings(**{k: v for k, v in _data.items() if k in MyAppSettings.model_fields})
"""
import json
import os
from typing import List

from pydantic import BaseModel
from pydantic_settings import BaseSettings, SettingsConfigDict


# ---------------------------------------------------------------------------
# Settings loader
# ---------------------------------------------------------------------------

def load_json_settings(base_dir: str | None = None) -> dict:
    """
    Loads appsettings.json and merges appsettings.{ASPNETCORE_ENVIRONMENT}.json
    on top of it (shallow merge per top-level key, deep merge for nested dicts).

    Args:
        base_dir: Directory containing the appsettings*.json files.
                  Defaults to the caller's package directory.
    """
    if base_dir is None:
        base_dir = os.path.dirname(os.path.abspath(__file__))

    env = os.environ.get("ASPNETCORE_ENVIRONMENT", "Development")
    base_path = os.path.join(base_dir, "appsettings.json")
    env_path = os.path.join(base_dir, f"appsettings.{env}.json")

    settings_dict: dict = {}

    if os.path.exists(base_path):
        with open(base_path, "r", encoding="utf-8") as f:
            settings_dict.update(json.load(f))

    if os.path.exists(env_path):
        with open(env_path, "r", encoding="utf-8") as f:
            env_settings: dict = json.load(f)
            for key, value in env_settings.items():
                if (
                    isinstance(value, dict)
                    and key in settings_dict
                    and isinstance(settings_dict[key], dict)
                ):
                    settings_dict[key].update(value)
                else:
                    settings_dict[key] = value

    return settings_dict


# ---------------------------------------------------------------------------
# Shared settings models (all services need these sections)
# ---------------------------------------------------------------------------

class DatabaseSettings(BaseModel):
    Provider: str
    ConnectionString: str


class JwtSettings(BaseModel):
    Secret: str
    Issuer: str
    Audience: str
    AccessTokenExpirationMinutes: int
    RefreshTokenExpirationDays: int


class ServiceCommunicationSettings(BaseModel):
    Enabled: bool
    ServiceName: str
    SharedSecret: str
    AllowedServices: List[str] = []


class CorsSettings(BaseModel):
    AllowedOrigins: List[str] = []


class LoggingLevelSettings(BaseModel):
    Default: str = "Information"


class LoggingSettings(BaseModel):
    LogLevel: LoggingLevelSettings = LoggingLevelSettings()
    FilePath: str = "Logs"


# ---------------------------------------------------------------------------
# Base settings class — extend this per service
# ---------------------------------------------------------------------------

class BaseAppSettings(BaseSettings):
    """
    Base settings shared across all Python microservices.
    Extend this class in each service to add service-specific sections.
    """
    Urls: str = "http://localhost:5000"
    AllowedHosts: str = "*"
    DatabaseSettings: DatabaseSettings
    Jwt: JwtSettings
    ServiceCommunication: ServiceCommunicationSettings
    Cors: CorsSettings = CorsSettings()
    Logging: LoggingSettings = LoggingSettings()
    model_config = SettingsConfigDict(env_nested_delimiter="__")
