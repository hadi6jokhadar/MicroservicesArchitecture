import os

from pydantic import BaseModel

from ihsandev_shared.config import BaseAppSettings, load_json_settings


# ---------------------------------------------------------------------------
# AI-service-specific settings sections
# ---------------------------------------------------------------------------

class FileManagerSettings(BaseModel):
    BaseUrl: str


# ---------------------------------------------------------------------------
# Full settings for the AI service (extends the shared base)
# ---------------------------------------------------------------------------

class AppSettings(BaseAppSettings):
    FileManagerSettings: FileManagerSettings


# Load appsettings.json + appsettings.{env}.json from the AI.API root directory
_base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_settings_data = load_json_settings(_base_dir)
settings = AppSettings(**{k: v for k, v in _settings_data.items() if k in AppSettings.model_fields})
