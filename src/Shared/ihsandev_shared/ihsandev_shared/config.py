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

from pydantic import BaseModel, Field, field_validator, ValidationInfo
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


_KNOWN_PLACEHOLDER_SECRETS = {
    "change_me_jwt_secret",
    "change_me_shared_secret",
    "your-secret-key-here-min-32-chars-for-production-use",
}


def _validate_secret_strength(secret: str, field_name: str) -> str:
    """Fails fast at startup if a secret is missing, too short, or a committed
    placeholder. Mirrors JwtAuthenticationExtensions.ValidateSecretStrength (.NET)."""
    if not secret:
        raise ValueError(f"{field_name} is not configured.")
    if secret.strip().lower() in _KNOWN_PLACEHOLDER_SECRETS:
        raise ValueError(
            f"{field_name} is still set to a committed placeholder value. "
            f"Set a real, unique secret (at least 32 bytes) in the environment's .env/secrets."
        )
    if len(secret.encode("utf-8")) < 32:
        raise ValueError(f"{field_name} must be at least 32 bytes (256 bits).")
    return secret


class JwtSettings(BaseModel):
    Secret: str
    Issuer: str
    Audience: str
    AccessTokenExpirationMinutes: int
    RefreshTokenExpirationDays: int

    @field_validator("Secret")
    @classmethod
    def _check_secret(cls, v: str) -> str:
        return _validate_secret_strength(v, "Jwt.Secret")


class ServiceCommunicationSettings(BaseModel):
    Enabled: bool
    ServiceName: str
    SharedSecret: str
    AllowedServices: List[str] = []

    @field_validator("SharedSecret")
    @classmethod
    def _check_shared_secret(cls, v: str, info: ValidationInfo) -> str:
        if not info.data.get("Enabled", True):
            return v
        return _validate_secret_strength(v, "ServiceCommunication.SharedSecret")


class CorsSettings(BaseModel):
    AllowedOrigins: List[str] = []

    @field_validator("AllowedOrigins")
    @classmethod
    def _check_allowed_origins(cls, v: List[str]) -> List[str]:
        # CORSMiddleware is always wired with allow_credentials=True (main.py), and
        # Starlette reflects the request Origin instead of a literal "*" whenever
        # credentials are on — an empty list falling back to ["*"] would let any
        # origin make authenticated cross-site requests. Fail fast instead.
        if not v:
            raise ValueError(
                "Cors.AllowedOrigins is not configured. It must list at least one "
                "explicit origin — it cannot be empty while allow_credentials=True."
            )
        return v


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
    Cors: CorsSettings
    Logging: LoggingSettings = LoggingSettings()
    model_config = SettingsConfigDict(env_nested_delimiter="__")
