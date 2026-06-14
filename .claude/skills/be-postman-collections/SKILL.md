---
name: be-postman-collections
description: Generate or refresh Postman collections from .NET endpoint source code — scans MapGet/Post/Put/Delete calls, produces Postman v2.1 JSON files per service or as a unified gateway collection. Use this whenever the user asks to generate Postman collections, update API collections, refresh Postman files, export API docs as Postman, or create API test collections for any service.
---

# Generate Postman Collections From Endpoints

## Required Inputs

1. Target: service name, `all`, or `gateway` (unified collection through port 5000)
2. Base URL override per service if different from appsettings
3. Write mode: `replace` (full regeneration) or `merge` (preserve manual examples)

## Discovery Phase

1. Read `PostmanCollections/README.md`
2. Read each target service's `Program.cs` to find configured port and route conventions
3. For `gateway` or `all`: read `src/Gateway/Gateway.API/appsettings.json` for route-to-cluster mappings
4. Scan endpoint files for:
   - HTTP methods: `MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapPatch`
   - Route prefixes from `MapGroup(...)`
   - Auth requirements from `RequireAuthorization` / allow-anonymous
   - Tenant behavior hints from attributes

## Collection Rules

- Schema version: `v2.1.0`
- Individual service collection name: `{ServiceName} Service API`
- Gateway collection name: `Gateway API (Unified)`
- Variables: `baseUrl`, `tenantId`, `authToken`, `refreshToken`, `serviceSecret`
- Individual service folders: `Public`, `Authenticated`, `Admin`, `Internal`
- Gateway: grouped by service name, then concern — exclude `Internal` and SignalR hubs

### AI Service Path Transform (Gateway only)

Gateway routes `/api/v1/ai/{**}` and YARP transforms to `/api/v1/{**}` on the AI service:

- `/api/v1/settings/` → gateway: `/api/v1/ai/settings/`
- `/api/v1/chat/stream` → gateway: `/api/v1/ai/chat/stream`

## Header Conventions

Add headers only when applicable:
- `Authorization: Bearer {{authToken}}`
- `x-tenant-id: {{tenantId}}`
- `X-Service-Secret: {{serviceSecret}}`
- `Content-Type: application/json` for JSON body requests

## Output

- Write/update each collection JSON file in `PostmanCollections/`
- Keep deterministic ordering: folder name → request path → method order (GET, POST, PUT, PATCH, DELETE)
- Validate produced JSON is valid and importable

## Quality Bar

- No guessed endpoints
- No missing route parameters
- No invalid JSON
- No duplicate request names within the same folder
