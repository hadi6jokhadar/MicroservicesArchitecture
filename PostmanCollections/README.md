# Postman Collections for Microservices Architecture

This directory contains Postman collections for all the microservices in the architecture. Each service has its own collection file that can be imported into Postman.

## Auto Generation Workflow

You can generate or refresh collections directly from endpoint source code using the custom Copilot prompt:

- `.github/prompts/generate_postman_collections.prompt.md`

Use it when endpoint files change and you want collection files to stay aligned with real routes.
The workflow scans `src/Services/*/*.API/Endpoints/**/*.cs` and updates files in this folder.

## When to Use Which Collection

| Collection                                    | Base URL                | Use when                                                       |
| --------------------------------------------- | ----------------------- | -------------------------------------------------------------- |
| **`Gateway_Service.postman_collection.json`** | `http://localhost:5000` | Integration testing, end-to-end flows, production-like testing |
| Individual service collections                | `http://localhost:500X` | Isolating a single service, debugging, development             |

The gateway collection routes every request through YARP. Individual service collections bypass the gateway and hit services directly.

> **Note:** Internal service-to-service endpoints (using `X-Service-Secret`) and SignalR hubs are only available in individual service collections — they are not routed through the gateway.

## Collections

### Gateway (Unified) (`Gateway_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5000`
- **Covers**: All 8 services through the API Gateway (YARP)
- **Excludes**: Internal (X-Service-Secret) endpoints and SignalR hubs
- **AI routes**: Uses `/api/v1/ai/` prefix — YARP transforms to `/api/v1/` before forwarding to port 5008

### 0. AI Service (`AI_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5008`
- **Endpoints**:
  - Health check
  - AI provider settings full CRUD
  - System prompts full CRUD
  - Chat streaming

### 1. FileManager Service (`FileManager_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5005`
- **Endpoints**:
  - File upload/download operations
  - File metadata management
  - Admin operations (global access)
  - Internal service-to-service endpoints

### 2. Identity Service (`Identity_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5001`
- **Endpoints**:
  - Authentication (login, register, refresh token, logout)
  - Profile management
  - Admin user management
  - Device token management

### 3. Notification Service (`Notification_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5004`
- **Endpoints**:
  - Send notifications (service/admin)
  - User notification management
  - Queue status and management
  - SignalR hub connection

### 4. Tenant Service (`Tenant_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5002`
- **Endpoints**:
  - Tenant configuration retrieval
  - Tenant management (admin only)
  - Public tenant information

### 5. Translation Service (`Translation_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5006`
- **Endpoints**:
  - Get translations (public, with optional tenant-specific overrides)
  - Translation key management (admin only)
  - Set/update translation values (global and tenant-specific)
  - Bulk import translations
  - Multi-language support (global and per-tenant customization)

### 6. Nasheed Service (`Nasheed_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5009`
- **Endpoints**:
  - Artist and song management
  - Ingestion job monitoring and reindex operations
  - Semantic search and similar song retrieval
  - Favorites, ratings, and play interactions
  - Lyrics generation endpoint

## Setup Instructions

1. **Import Collections**:
   - Open Postman
   - Click "Import" button
   - Select "File" tab
   - Choose the collection JSON files from this directory

2. **Configure Variables**:
   Each collection has predefined variables that need to be set:
   - `baseUrl`: Service URL (already set to correct localhost ports)
   - `tenantId`: Your tenant identifier (update this)
   - `authToken`: JWT token for authentication (obtain from login endpoints)
   - `refreshToken`: Refresh token (obtain from login/register)
   - `serviceSecret`: Shared secret for service-to-service communication

3. **Authentication Flow**:
   - Start with Identity Service collection
   - Use "Login" or "Register" endpoint to get tokens
   - Set the `authToken` variable with the access token
   - Set the `refreshToken` variable if needed
   - Use authenticated endpoints in other services

## Multi-Tenancy

This architecture supports multi-tenancy. Most endpoints require:

- `x-tenant-id` header: Identifies which tenant's data to access
- Appropriate JWT token: Either global (for service operations) or tenant-specific

## Service-to-Service Communication

Some endpoints use `X-Service-Secret` header instead of JWT for internal service communication.

## Notes

- All collections include example request bodies with placeholder data
- Update variable values according to your environment
- Some endpoints may require specific roles (User, Admin, SuperAdmin, Service)
- Rate limiting is split: Global (10,000 req/min) and PerIP (500 req/min/IP) enforced at the gateway; PerTenant and PerUser enforced at individual services
- File upload endpoints expect multipart/form-data

## Testing Workflow

1. Start all services locally
2. Import collections into Postman
3. Set tenant ID variable
4. Use Identity service to authenticate
5. Test other services with obtained tokens
6. Use admin endpoints for management operations

## Environment Setup

Make sure your services are running on the specified ports:

- Identity: 5001
- Tenant: 5002
- Notification: 5004
- FileManager: 5005
- Translation: 5006
- Nasheed: 5009

You can use the provided batch files in the services directories to start them.
