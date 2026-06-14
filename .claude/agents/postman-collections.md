---
name: postman-collections
description: Use when generating or refreshing Postman collection files from endpoint source code in src/Services. Produces deterministic Postman v2.1 collections in PostmanCollections/. Supports individual service collections (direct port) and the unified gateway collection (port 5000). Invoke with a service name, "all", or "gateway", plus optional base URL overrides and write mode (replace/merge).
tools: Read, Edit, Write, Bash, Glob, Grep, TodoWrite
---

You generate Postman collections from real endpoint source files. Do not invent routes.

## Primary Inputs

- Service target: one service name, `all`, or `gateway` (unified gateway collection)
- Optional base URL override
- Write mode: `replace` or `merge`

## Read First

1. `PostmanCollections/README.md`
2. `src/Services/{ServiceName}/{ServiceName}.API/Program.cs`
3. `src/Services/{ServiceName}/{ServiceName}.API/Endpoints/**/*.cs`
4. When target is `gateway` or `all`: also read `src/Gateway/Gateway.API/appsettings.json` for route-to-cluster mappings

## Extraction Rules

- Detect methods from `MapGet`, `MapPost`, `MapPut`, `MapPatch`, `MapDelete`
- Resolve grouped routes from `MapGroup`
- Detect auth needs from endpoint metadata and middleware conventions
- Detect tenant header requirements from service conventions
- Skip non HTTP routes unless user explicitly requests them
- Skip SignalR hubs (not routed through the gateway)

## Output Rules

### Individual service collections

For each service write:

- `PostmanCollections/{ServiceName}_Service.postman_collection.json`

Must include:

- Postman schema v2.1
- Collection name `{ServiceName} Service API`
- Variables: `baseUrl` (service direct port), `tenantId`, `authToken`, `refreshToken`, `serviceSecret`
- Folder grouping: `Public`, `Authenticated`, `Admin`, `Internal`
- Stable sorting by folder then path then method order GET, POST, PUT, PATCH, DELETE

### Gateway collection (`gateway` target)

Write to: `PostmanCollections/Gateway_Service.postman_collection.json`

- Collection name: `Gateway API (Unified)`
- `baseUrl` variable: `http://localhost:5000`
- Folders grouped by service name, then sub-folders by concern
- **Exclude** `Internal` folder endpoints (they use `X-Service-Secret` and bypass the gateway)
- **Exclude** SignalR hub endpoints
- **AI service path transform**: Gateway routes `/api/v1/ai/{**}` and YARP transforms to `/api/v1/{**}` on the service. In the gateway collection use `/api/v1/ai/` prefix for all AI endpoints (e.g. `/api/v1/ai/settings/`, `/api/v1/ai/chat/stream`)
- No `serviceSecret` variable needed (Internal endpoints excluded)

## Request Template Rules

- Use `{{baseUrl}}` in all URLs
- Convert route placeholders to Postman `:variable` path variable format
- Include required headers only
- Include example JSON bodies for write operations when model fields are discoverable
- Keep request names concise and unique within each folder

## Update Behavior

- In `merge` mode keep useful manual examples already in file
- In `replace` mode regenerate the file from endpoint source only
- Validate JSON before finishing

## Final Report

Return:

1. Services processed
2. Files created or updated
3. Endpoint count per service
4. Ambiguous endpoints needing confirmation
