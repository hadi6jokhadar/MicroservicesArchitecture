import pytest
import pytest_asyncio
from httpx import AsyncClient, ASGITransport
import uuid
from unittest.mock import AsyncMock, MagicMock

from main import app
from api.dependencies import get_db, require_auth, get_tenant_id

# Mock dependencies
async def override_require_auth():
    return {"role": "User", "payload": {"sub": str(uuid.uuid4())}}

async def override_get_tenant_id():
    return str(uuid.uuid4())

@pytest.fixture
def mock_db_session(mocker):
    # Create a full mock of AsyncSession
    mock_session = AsyncMock()
    
    # Setup for `.execute(select(...)).scalars().all()`
    mock_scalars = mocker.MagicMock()
    mock_execute_result = mocker.MagicMock()
    mock_execute_result.scalars.return_value = mock_scalars
    mock_session.execute = mocker.AsyncMock(return_value=mock_execute_result)
    
    # Basic mocks for add, commit, refresh
    mock_session.add = mocker.MagicMock()
    mock_session.commit = mocker.AsyncMock()
    mock_session.refresh = mocker.AsyncMock()

    # Apply overriding
    async def override_get_db():
        yield mock_session
        
    app.dependency_overrides[get_db] = override_get_db
    
    # Store the result mock chain so tests can attach return values
    mock_session.mock_scalars = mock_scalars
    
    return mock_session

@pytest.fixture(scope="session")
def auth_overrides():
    app.dependency_overrides[require_auth] = override_require_auth
    app.dependency_overrides[get_tenant_id] = override_get_tenant_id
    yield
    # Clean up overrides
    app.dependency_overrides.pop(require_auth, None)
    app.dependency_overrides.pop(get_tenant_id, None)

@pytest_asyncio.fixture()
async def client(auth_overrides) -> AsyncClient:
    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://testserver"
    ) as ac:
        yield ac
