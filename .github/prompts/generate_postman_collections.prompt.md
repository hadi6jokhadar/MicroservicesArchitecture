---
agent: "agent"
description: "Generate or update Postman collections by scanning service endpoint files in src/Services and writing normalized collections into PostmanCollections/."
---

# Generate Postman Collections From Endpoints

Use this workflow when the user asks to create or refresh Postman collections from service endpoint source code.

## Goal

Create accurate Postman collection files for one service or all services by scanning endpoint definitions in:

- `src/Services/{ServiceName}/{ServiceName}.API/Endpoints/**/*.cs`

Collections are written to:

- `PostmanCollections/{ServiceName}_Service.postman_collection.json`

## Required Inputs

Ask only if missing:

1. Target service name or `all`
2. Base URL override per service if different from appsettings
3. Whether to replace full collection or merge with existing file

## Discovery Phase

1. Read `PostmanCollections/README.md`.
2. Read each target service API `Program.cs` to find configured port and route conventions.
3. Scan endpoint files for:
   - HTTP methods: `MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapPatch`
   - Route prefixes from `MapGroup(...)`
   - Endpoint metadata such as `WithName`, `RequireAuthorization`, or allow-anonymous usage
   - Tenant behavior hints from attributes or middleware usage in service docs
4. Keep only callable HTTP endpoints. Skip SignalR hubs unless user requested them.

## Collection Rules

1. Collection schema version must be `v2.1.0`.
2. Collection name must follow `{ServiceName} Service API`.
3. Add variables when needed:
   - `baseUrl`
   - `tenantId`
   - `authToken`
   - `refreshToken` when auth flow exists
   - `serviceSecret` for internal service endpoints
4. Group requests into folders:
   - `Public`
   - `Authenticated`
   - `Admin`
   - `Internal`
5. For each request include:
   - Method
   - URL with route params as Postman variables
   - Required headers
   - Example JSON body for POST, PUT, PATCH when body model exists
   - A concise description copied from endpoint intent if available

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
