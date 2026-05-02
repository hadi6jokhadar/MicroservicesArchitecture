# Nasheed Service — Documentation Index

**Purpose:** Entry point for all Nasheed service documentation. Read this first before any task.  
**Location:** `src/Apps/Nasheed/`  
**Last Updated:** May 2, 2026

---

## 📖 How to Use This Index

1. **Read this file first** — always start here
2. **Find your topic** — use Ctrl+F to search for keywords
3. **Read only the relevant file** — each file is self-contained

---

## 📄 Documentation Files

### OVERVIEW.md

**Description:** Service purpose, port, Clean Architecture layers, database strategy, tenant model, key design decisions, and startup sequence.  
**Read When:**

- Starting any Nasheed task
- Onboarding to the Nasheed service
- Understanding how Nasheed fits into the platform

### ENTITIES_AND_DATA_MODEL.md

**Description:** All domain entities, their fields, enums, relationships, and EF Core configuration notes. Includes the ingestion job lifecycle state machine.  
**Read When:**

- Working with domain entities
- Writing queries or migrations
- Understanding data relationships

### API_ENDPOINTS.md

**Description:** Full endpoint reference — all routes, HTTP methods, request shapes, response shapes, and authentication requirements.  
**Read When:**

- Implementing or calling any Nasheed API endpoint
- Building a Postman collection for Nasheed
- Adding new endpoints

### INGESTION_PIPELINE.md

**Description:** Background ingestion worker design — job types, processing stages (metadata extraction → lyrics verification → embedding generation), retry logic, and the `NasheedTenantLoaderService` startup sequence.  
**Read When:**

- Working on the ingestion worker
- Understanding job state transitions
- Debugging stuck or failing jobs

### AI_INTEGRATION.md

**Description:** How Nasheed calls AI.API — settings keys, prompt keys, required AI.API DB entries for each stage (extraction, verification, embedding, generation).  
**Read When:**

- AI calls are failing (401/404)
- Setting up AI.API for a new tenant
- Adding a new AI stage

---

## 🔗 Related Global Documentation

- `Doc/NASHEED_LIBRARY_BACKEND.md` — original design plan
- `Doc/AI_SERVICE_CHAT_INTEGRATION_GUIDE.md` — how to call AI.API from .NET
- `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md` — service creation guide (covers `src/Apps/` folder rules)
- `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md` — Strategy B per-tenant DB pattern
- `Doc/SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md` — X-Service-Secret header auth
