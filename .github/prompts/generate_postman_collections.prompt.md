---
agent: "agent"
description: "Generate or update Postman collections by scanning service endpoint files in src/Services and writing normalized collections into PostmanCollections/."
---

# Generate Postman Collections From Endpoints

Use this workflow when the user asks to create or refresh Postman collections from service endpoint source code.

## Goal

Create accurate Postman collection files for one service, all services, or the unified gateway collection by scanning endpoint definitions in:

- `src/Services/{ServiceName}/{ServiceName}.API/Endpoints/**/*.cs`
- `src/Apps/{AppName}/{AppName}.API/Endpoints/**/*.cs` (for apps like Nasheed)

Collections are written to:

- `PostmanCollections/{ServiceName}_Service.postman_collection.json` (per service)
- `PostmanCollections/Gateway_Service.postman_collection.json` (unified gateway)

## Required Inputs

Ask only if missing:

1. Target: service name, `all`, or `gateway` (unified collection through port 5000)
2. Base URL override per service if different from appsettings
3. Whether to replace full collection or merge with existing file

## Discovery Phase

1. Read `PostmanCollections/README.md`.
2. Read each target service API `Program.cs` to find configured port and route conventions.
3. When target is `gateway` or `all`: also read `src/Gateway/Gateway.API/appsettings.json` to extract route-to-cluster mappings and any path transforms.
4. Scan endpoint files for:
   - HTTP methods: `MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapPatch`
   - Route prefixes from `MapGroup(...)`
   - Endpoint metadata such as `WithName`, `RequireAuthorization`, or allow-anonymous usage
   - Tenant behavior hints from attributes or middleware usage in service docs
5. Keep only callable HTTP endpoints. Skip SignalR hubs unless user requested them.

## Collection Rules

1. Collection schema version must be `v2.1.0`.
2. For individual service collections, name must follow `{ServiceName} Service API`.
3. For the gateway collection, name must be `Gateway API (Unified)`.
4. Add variables when needed:
   - `baseUrl` — service direct port for individual collections; `http://localhost:5000` for gateway collection
   - `tenantId`
   - `authToken`
   - `refreshToken` when auth flow exists
   - `serviceSecret` for internal service endpoints (individual collections only — not in gateway collection)
5. Individual service collections: group requests into folders `Public`, `Authenticated`, `Admin`, `Internal`
6. Gateway collection: group by service name at top level, then by concern sub-folder; exclude `Internal` folder and SignalR hubs entirely
7. For each request include:
   - Method
   - URL with route params as Postman `:variable` path variable format
   - Required headers
   - Example JSON body for POST, PUT, PATCH when body model exists
   - A concise description copied from endpoint intent if available

### AI Service Gateway Path Transform

The gateway routes `/api/v1/ai/{**}` and YARP transforms the path to `/api/v1/{**}` when forwarding to the AI service on port 5008.
In the **gateway collection** use the gateway-side path with `/api/v1/ai/` prefix:

- Service path `/api/v1/settings/` → gateway path `/api/v1/ai/settings/`
- Service path `/api/v1/chat/stream` → gateway path `/api/v1/ai/chat/stream`
- Service path `/api/v1/prompts/` → gateway path `/api/v1/ai/prompts/`

## Header Conventions

Add headers only when applicable:

- `Authorization: Bearer {{authToken}}`
- `x-tenant-id: {{tenantId}}`
- `X-Service-Secret: {{serviceSecret}}`
- `Content-Type: application/json` for JSON body requests

## Output Behavior

1. Write or update each collection JSON file.
2. Preserve manual examples already present unless user asked for full replace.
3. Keep deterministic ordering:
   - Folder name ascending
   - Request path ascending
   - Method order: GET, POST, PUT, PATCH, DELETE
4. Validate produced JSON is valid and importable.

## Post Update Steps

1. If endpoint coverage changed materially, update `PostmanCollections/README.md` endpoint notes.
2. Return a summary with:
   - Services processed
   - Files created or updated
   - Number of endpoints mapped per service
   - Any ambiguous endpoints that need user confirmation

## Quality Bar

- No guessed endpoints.
- No missing route parameters.
- No invalid JSON.
- No duplicate request names inside the same folder.
- Use repository conventions for tenant and auth headers.
