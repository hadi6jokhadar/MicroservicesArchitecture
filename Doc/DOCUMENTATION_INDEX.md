# Documentation Index - AI Agent Entry Point

**🎯 START HERE** - This is the **ONLY** file AI agents need to read first.

**Purpose:** Single source of truth for what documentation exists and when to read each file.  
**Last Updated:** June 6, 2026  
**Total Files:** 39

---

## 🗂️ Project Folder Structure

The `src/` directory has two distinct sub-folders:

| Folder          | Role                                                                   | Current projects                                             |
| --------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------ |
| `src/Services/` | Core platform microservices. Foundational — other Apps depend on them. | Identity, Tenant, FileManager, Notification, Translation, Category, AI |
| `src/Apps/`     | Domain-specific application projects that consume platform Services.   | Nasheed                                                      |

**Every project in `src/Apps/` must have its own `Doc/` folder** with at minimum: `DOCUMENTATION_INDEX.md`, `OVERVIEW.md`, `ENTITIES_AND_DATA_MODEL.md`, `API_ENDPOINTS.md`. See `NEW_SERVICE_INTEGRATION_GUIDE.md` for the full required file list.

---

## 📖 How to Use This Index

### For AI Agents:

1. **Read this file FIRST** - Always start here
2. **Find your topic** - Use Ctrl+F to search for keywords
3. **Read ONLY relevant files** - Don't read everything
4. **One file = Complete information** - Each file contains everything about that topic

### File Organization:

Files are organized by category. Each entry includes:

- **File Name** - The actual filename
- **Description** - What the file contains
- **Read When** - When an AI should read this file

---

## 🔄 Architecture Patterns

### EVENT_DRIVEN_PUBLISHER_PATTERN.md

**Description:** Generic step-by-step guide for adding Redis Pub/Sub event publishing to **any** service. Covers: event message record, publisher interface, Redis implementation, no-op fallback, DI registration, and handler wiring. Use `{ServiceName}` placeholders throughout. Reference implementation is the Category service.  
**Read When:**

- Adding event publishing to a new or existing service
- A service needs to broadcast state changes for other services to consume
- Following up on `CATEGORY_EVENT_DRIVEN_CONSUMER_GUIDE.md` to implement the publisher side
- Replicating the Category service pub/sub pattern in another service

---

## 🏗️ Core Architecture (READ FIRST FOR NEW AI AGENTS)

### DATABASE_PER_TENANT_ARCHITECTURE.md

**Description:** Complete multi-tenancy architecture explanation. Each tenant gets separate database. Core pattern for entire system.  
**Read When:**

- Starting any backend task
- Understanding tenant isolation
- Working with any service
- Need to understand how data is stored

### AUTOMATIC_DATABASE_MIGRATION.md

**Description:** How databases are automatically created and migrated per tenant on first request.  
**Read When:**

- Database isn't being created
- Adding migrations
- Understanding tenant onboarding
- Deploying new services

### SHARED_IDENTITY_SERVICE_GUIDE.md

**Description:** Complete JWT authentication, user management, login/registration flows. Used by ALL services.  
**Read When:**

- Implementing authentication
- Working with users
- Understanding JWT tokens
- Adding new endpoints that need auth
- User login/registration issues

### MULTI_TENANCY_GUIDE.md

**Description:** How multi-tenancy works across all services. Tenant resolution, database switching, optional vs required tenant context.  
**Read When:**

- Creating new service
- Understanding tenant isolation
- Working with tenant-specific data
- Implementing global vs tenant endpoints

### TENANT_MIDDLEWARE_EXPLAINED.md

**Description:** How TenantMiddleware resolves tenant from x-tenant-id header and switches database connections.  
**Read When:**

- Debugging tenant resolution issues
- Understanding middleware pipeline
- Adding custom middleware
- Tenant not being detected

---

## 🆕 Creating New Services

### NEW_SERVICE_INTEGRATION_GUIDE.md

**Description:** Complete step-by-step guide to create a new microservice. Project structure, multi-tenancy setup, database context, DI registration. Also explains the `src/Services/` vs `src/Apps/` folder distinction and the per-app `Doc/` folder requirement.  
**Read When:**

- Creating a brand new service or app
- Copying service structure
- Setting up Clean Architecture layers
- Need microservice boilerplate
- Deciding where to put a new project (`Services/` vs `Apps/`)

### SERVICE_INTEGRATION_TEST_GUIDE.md

**Description:** Step-by-step recipe for creating integration tests for any service. Covers project creation, `CustomWebApplicationFactory` pattern, MediatR handler testing (why HTTP layer is bypassed), test file structure, all test patterns (happy path / not-found / validation / side-effects), adding to solution, and README requirements.  
**Read When:**

- Adding integration tests to a service for the first time
- Following up on a "create tests" request
- Deciding what to stub vs what to test
- Looking for test code pattern examples

---

## 🎵 Nasheed Library (`src/Apps/Nasheed/`)

> Located at `src/Apps/Nasheed/` — a domain app that consumes platform Services (AI, FileManager, Tenant).  
> **All Nasheed documentation lives in `src/Apps/Nasheed/Doc/`** — see its own `DOCUMENTATION_INDEX.md`.  
> Files: `OVERVIEW.md`, `ENTITIES_AND_DATA_MODEL.md`, `API_ENDPOINTS.md`, `INGESTION_PIPELINE.md`, `AI_INTEGRATION.md`.

> ⚠️ `NASHEED_LIBRARY_BACKEND.md` and `NASHEED_LIBRARY_FRONTEND.md` **no longer exist** in this Doc folder. Those were design-phase documents that have been superseded by the implemented docs in `src/Apps/Nasheed/Doc/`.

---

## 🌐 Infrastructure

### API_GATEWAY_GUIDE.md

**Description:** Complete guide for the YARP-based API Gateway (`src/Gateway/Gateway.API/`). Covers service routing table (all 8 downstream services), admin endpoint conflict resolution, AI stream route special handling (10-min timeout, SSE), rate limiting (500 req/min per IP), end-to-end correlation ID chain (gateway inject → service middleware → frontend interceptor), health endpoints (`/health` lightweight + `/health/aggregate` polling all downstream services), run instructions, and future work.  
**Read When:**

- Configuring or modifying the gateway routing table
- Debugging request routing failures
- Adding a new service that needs to be reachable through the gateway
- Understanding why an internal service must NOT call through the gateway
- Changing rate limits or correlation ID behavior
- Checking downstream service health via the gateway aggregate endpoint
- Pointing the frontend at `http://localhost:5000` (gateway base URL)

---

## 🗛 Platform Roadmap

### PLATFORM_CAPABILITIES_ROADMAP.md

**Description:** Actionable implementation guide for 12 missing platform capabilities, organized in three priority tiers: Tier 1 (API Gateway ✅, Distributed Tracing ✅ including health checks + correlation ID, Secrets Management, Circuit Breaker, Audit Logging ✅), Tier 2 (Background Jobs, API Versioning, Feature Flags, DB Backup), Tier 3 (Search, CDN, Usage Metering). Each item includes NuGet packages, code samples, affected services, and a checklist.  
**Read When:**

- Planning new infrastructure work
- Deciding what to build next for production readiness
- Starting implementation of any of the 12 capabilities listed

### OBSERVABILITY_GUIDE.md

**Description:** Complete guide for the distributed tracing and metrics stack (OpenTelemetry → Jaeger + Prometheus + Grafana). Covers how traces flow from all 7 services (6 .NET + 1 Python) to Jaeger, how Prometheus scrapes `/metrics` from each service, how to start the observability stack with `docker compose`, how to verify traces and metrics in Jaeger UI and Prometheus, and Grafana setup steps. Also covers health check endpoints (`/health` + `/health/ready`) on every service and end-to-end `X-Correlation-Id` propagation (gateway → service middleware → frontend interceptor → logs). Includes architecture diagram, troubleshooting table, and production notes.  
**Read When:**

- Starting or stopping the Jaeger/Prometheus/Grafana observability stack
- Debugging missing traces or Prometheus scrape failures
- Adding observability instrumentation to a new service
- Understanding what is automatically instrumented (HTTP in/out, EF Core, SQLAlchemy)
- Setting up Grafana dashboards or data sources
- Understanding how `X-Correlation-Id` flows from browser to backend logs
- Verifying service health checks are working

---

## 🗂️ Category Service

### CATEGORY_SERVICE_GUIDE.md

**Description:** Complete reference for the Category microservice. Hierarchical tree (materialized path), CRUD + move operation, localized names (JSONB), optional icon/banner images via File Manager, Redis caching, database-per-tenant with optional tenant context, admin bypass endpoints, event publishing via Transactional Outbox pattern (`OutboxCategoryEventPublisher` + `OutboxEventProcessorService`), and unit/integration tests.  
**Read When:**

- Working on or consuming the Category service
- Understanding the materialized-path tree model
- Implementing the move-category operation
- Debugging stale cache or missing file enrichment
- Adding or consuming category admin endpoints
- Understanding how category events are published to Redis (Outbox pattern)

### CATEGORY_EVENT_DRIVEN_CONSUMER_GUIDE.md

**Description:** Step-by-step guide for any service that needs categories without calling the Category service at runtime. Covers: Redis Pub/Sub event format, `CategorySnapshotEntity` setup, EF migration, background subscriber service, first-deployment backfill, and handling Move events.  
**Read When:**

- Building a new service (e.g. Item, Product, Song) that needs categories
- Implementing the event-driven local snapshot pattern
- Understanding what events the Category service publishes and on which Redis channels
- Debugging stale category snapshots in a consumer service

---

## AI Service (Python)

### AI_SERVICE_OVERVIEW.md

**Description:** Full architecture and operational overview for the AI Python service, including endpoints, auth modes, tenant handling, startup behavior, and runtime flow.  
**Read When:**

- Understanding AI service architecture
- Onboarding to AI.API codebase
- Working on AI endpoint behavior
- Troubleshooting service startup or routing

### AI_SERVICE_CHAT_INTEGRATION_GUIDE.md

**Description:** How any .NET microservice can call the AI service chat endpoints (`/api/v1/chat/single` or `/api/v1/chat/stream`) using service-to-service authentication. Covers required request field (`settings_key`), optional `system_prompt_key` and `messages` with at-least-one validation, optional `max_completion_tokens` and `generate_session_title`, optional tenant and file attachment support, HttpClient registration, snake_case serialization, and error handling.  
**Read When:**

- A .NET service needs to perform AI tasks internally (summarize, generate, classify)
- Integrating AI chat into a non-AI service
- Troubleshooting 401/403/404 errors when calling AI from another service
- Understanding what must exist in the AI DB before calling the endpoint

### AI_SERVICE_MIGRATION_GUIDE.md

**Description:** How AI.API handles database creation, Alembic upgrades, and schema bootstrap. Includes migration workflow and troubleshooting for missing tables and model changes.  
**Read When:**

- Debugging migration or startup database issues
- Seeing relation does not exist errors
- Changing ORM model schema
- Creating Alembic revisions

### PYTHON_SHARED_LIBRARY_GUIDE.md

**Description:** Documentation for the shared Python package (`ihsandev_shared`) that powers config loading, auth, exceptions, logging, DB utilities, and service clients.  
**Read When:**

- Modifying shared Python modules
- Integrating new Python services
- Understanding shared auth and error handling behavior
- Troubleshooting shared package behavior

---

## 🔐 Authentication & Authorization

### ROLES_AND_CLAIMS_GUIDE.md

**Description:** Database-driven roles and claims system. Role management endpoints, Redis caching, SuperAdmin/Admin/User roles, custom permissions.  
**Read When:**

- Working with user roles
- Implementing authorization
- Managing permissions
- Creating role-based features
- Understanding claims system

---

## 🔒 Admin & Bypass Endpoints

### BYPASS_TENANT_ENDPOINTS_GUIDE.md

**Description:** How to create admin endpoints that work WITHOUT x-tenant-id header. Global database access, BypassTenantAttribute, dual migration strategy.  
**Read When:**

- Creating admin endpoints
- Need global data access across all tenants
- SuperAdmin functionality
- System-wide operations
- Understanding optional tenant context

---

## 📁 File Manager Service

### FILE_MANAGER.md

**Description:** Complete File Manager Service guide. File upload/download API, multi-tenancy, dual endpoints (tenant + admin), static file serving, Redis caching, background cleanup, service-to-service integration.  
**Read When:**

- Implementing file uploads/downloads
- Working with user files or documents
- Profile pictures or attachments
- Understanding file storage architecture
- Creating admin file endpoints
- Service-to-service file operations
- File lifecycle management
- Redis caching for tenant configs

> For HTTP client extensions used alongside FileManager, see `SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md` under **🔧 Development Patterns** below.

---

## 🔔 Notification Service

### NOTIFICATION_SERVICE_README.md

**Description:** Complete notification system: SignalR real-time notifications, Firebase push notifications, device tokens, tenant/user/global notifications.  
**Read When:**

- Implementing notifications
- Working with SignalR
- Push notifications
- Real-time updates
- User notifications

### FIREBASE_PUSH_NOTIFICATIONS_GUIDE.md

**Description:** Firebase Cloud Messaging integration for push notifications. Device token management, global vs tenant vs user notifications.  
**Read When:**

- Implementing mobile push notifications
- Working with FCM
- Device token registration
- Send notifications to mobile devices

---

## 🌍 Translation Service

### TRANSLATION_SERVICE_GUIDE.md

**Description:** Multi-language translation system. Global database with tenant-specific overrides. Translation management, language support (en/ar).  
**Read When:**

- Implementing translations
- Adding new languages
- Managing translation keys
- Tenant-specific translations

### LOCALIZATION_GUIDE.md

**Description:** Complete i18n system for validation messages, error messages, field names. LocalizationService, resource files, FluentValidation integration.  
**Read When:**

- Localizing error messages
- Multi-language validation
- Adding new localized strings
- Working with resource files

---

## 👤 User Features

### PHONE_VERIFICATION_LOGIN_GUIDE.md

**Description:** OTP-based authentication via phone/email. Verification codes, SMS/email sending, security, dev mode testing.  
**Read When:**

- Implementing phone/email login
- OTP verification
- Passwordless authentication
- SMS/email verification codes

### DEVICE_TOKEN_MANAGEMENT_GUIDE.md

**Description:** Managing device tokens for push notifications. Registration, deactivation, tenant isolation.  
**Read When:**

- Working with device tokens
- Push notification setup
- Mobile device management
- FCM token handling

### PROFILE_PICTURE_COMPLETE_GUIDE.md

**Description:** Profile picture upload, FileManager integration, file lifecycle, batch fetching to prevent N+1 queries.  
**Read When:**

- Implementing profile pictures
- User avatars
- File upload integration
- Optimizing image queries

---

## ⚡ Performance & Caching

### CACHING_STRATEGY_COMPARISON.md

**Description:** Different caching strategies: Redis vs MemoryCache, distributed vs local, cache patterns.  
**Read When:**

- Choosing caching strategy
- Understanding cache trade-offs
- Performance architecture decisions

### PERFORMANCE_OPTIMIZATION_GUIDE.md

**Description:** Performance optimization techniques: database indexing, query optimization, parallel processing, rate limiting.  
**Read When:**

- Performance issues
- Slow queries
- Optimizing endpoints
- Scaling concerns

### USER_QUERY_OPTIMIZATION_IQUERYABLE.md

**Description:** IQueryable pattern for database-side pagination and filtering instead of loading everything into memory.  
**Read When:**

- Implementing pagination
- Large dataset queries
- N+1 query issues
- Database performance

---

## 🗄️ Database & Infrastructure

### DATABASE_REPLICATION_SETUP_GUIDE.md

**Description:** PostgreSQL master-slave replication setup for high availability and read scaling.  
**Read When:**

- Setting up database replication
- High availability requirements
- Read scaling
- Production deployment

---

## 🔧 Development Patterns

### SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md

**Description:** HTTP client extensions for service-to-service communication. Service secret authentication, IHttpClientFactory usage.  
**Read When:**

- Calling another microservice
- Service-to-service communication
- HttpClient configuration
- Internal API calls

### SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md

**Description:** Authentication between microservices using shared secrets. X-Service-Secret header, securing internal endpoints.  
**Read When:**

- Securing internal endpoints
- Service authentication
- Internal API security
- Preventing unauthorized service calls

### PROJECT_ISOLATION_STRATEGY_GUIDE.md

**Description:** Isolating user data by Project within same tenant. Soft isolation using ProjectId filter.  
**Read When:**

- Implementing project-based features
- Multi-project tenants
- Data isolation within tenant
- Understanding ProjectId vs TenantId

---

## ✅ Validation & Error Handling

### CENTRALIZED_VALIDATION_ERROR_HANDLING.md

**Description:** Centralized validation using FluentValidation, automatic error response formatting, localized error messages.  
**Read When:**

- Implementing validation
- Working with FluentValidation
- Error response formatting
- Input validation

---

## 🧪 Testing

### SHARED_TESTING_FILES.md

**Description:** Testing infrastructure, base test classes, WebApplicationFactory setup, test helpers, integration testing patterns.  
**Read When:**

- Writing tests
- Setting up test infrastructure
- Integration testing
- Test helpers needed

---

## 🛠️ Utilities & Tools

### CUSTOM_LOGGER_USAGE.md

**Description:** Logging best practices, ILogger usage, structured logging, log levels.  
**Read When:**

- Implementing logging
- Debugging issues
- Understanding log structure
- Log aggregation

---

## 📚 Special Documentation Files

### DOCUMENTATION_GUIDELINES.md

**Description:** **[READ THIS]** Complete guide for AI agents on how to create, update, and remove documentation. Anti-patterns, best practices, consolidation rules.  
**Read When:**

- Creating new documentation
- Updating existing docs
- About to make any .md file
- Need to consolidate duplicate docs

### README.md

**Description:** Project overview for humans. GitHub landing page. High-level architecture, tech stack, getting started.  
**Read When:**

- Need project overview
- Understanding tech stack
- Onboarding new humans (not AI)

### DOCUMENTATION_INDEX.md (this file)

**Description:** You are here. Index of all documentation.  
**Read When:** Always read this first

---

## 🎯 Quick Task Lookup

**Common tasks and which files to read:**

| Task                     | Files to Read                                                                                 |
| ------------------------ | --------------------------------------------------------------------------------------------- |
| Gateway routing          | API_GATEWAY_GUIDE.md                                                                          |
| Health checks            | API_GATEWAY_GUIDE.md (aggregate), OBSERVABILITY_GUIDE.md (per-service)                        |
| Correlation ID tracing   | API_GATEWAY_GUIDE.md, OBSERVABILITY_GUIDE.md                                                  |
| Observability / tracing  | OBSERVABILITY_GUIDE.md                                                                        |
| Create new service       | NEW_SERVICE_INTEGRATION_GUIDE.md, DATABASE_PER_TENANT_ARCHITECTURE.md, MULTI_TENANCY_GUIDE.md |
| Add authentication       | SHARED_IDENTITY_SERVICE_GUIDE.md                                                              |
| Implement file upload    | FILE_MANAGER.md                                                                               |
| Add notifications        | NOTIFICATION_SERVICE_README.md, FIREBASE_PUSH_NOTIFICATIONS_GUIDE.md                          |
| Add caching              | CACHING_STRATEGY_COMPARISON.md                                                                |
| Create admin endpoint    | BYPASS_TENANT_ENDPOINTS_GUIDE.md, SHARED_IDENTITY_SERVICE_GUIDE.md                            |
| Work on Nasheed service  | src/Apps/Nasheed/Doc/OVERVIEW.md, ENTITIES_AND_DATA_MODEL.md, API_ENDPOINTS.md                |
| Work on Category service | CATEGORY_SERVICE_GUIDE.md                                                                     |
| Work with roles          | ROLES_AND_CLAIMS_GUIDE.md, SHARED_IDENTITY_SERVICE_GUIDE.md                                   |
| Fix performance          | PERFORMANCE_OPTIMIZATION_GUIDE.md, USER_QUERY_OPTIMIZATION_IQUERYABLE.md                      |
| Add translations         | TRANSLATION_SERVICE_GUIDE.md, LOCALIZATION_GUIDE.md                                           |
| Service-to-service call  | SERVICE_TO_SERVICE_HTTP_CLIENT_EXTENSIONS.md, SERVICE_TO_SERVICE_AUTHENTICATION_GUIDE.md      |
| Database issue           | DATABASE_PER_TENANT_ARCHITECTURE.md, AUTOMATIC_DATABASE_MIGRATION.md                          |
| Understand AI service    | AI_SERVICE_OVERVIEW.md, AI_SERVICE_MIGRATION_GUIDE.md, PYTHON_SHARED_LIBRARY_GUIDE.md         |
| Write tests              | SHARED_TESTING_FILES.md                                                                       |
| Understand multi-tenancy | MULTI_TENANCY_GUIDE.md, DATABASE_PER_TENANT_ARCHITECTURE.md, TENANT_MIDDLEWARE_EXPLAINED.md   |

---

## 🚫 Files That Do NOT Exist (Prevent AI Hallucination)

AI agents: Do NOT reference or create these files - they have been removed:

- ❌ No "\*\_QUICK_REFERENCE.md" files (content merged into main guides)
- ❌ No "\*\_SUMMARY.md" files (temporary summaries removed)
- ❌ No "\*\_FIX.md" files (bug fix logs removed)
- ❌ No "\*\_MIGRATION.md" files (migration logs removed)
- ❌ No "\*\_STAGE_1/2/3.md" files (multi-part guides consolidated)
- ❌ No "00_START_HERE.md" (replaced by this file)
- ❌ No "NASHEED_LIBRARY_BACKEND.md" (superseded — use `src/Apps/Nasheed/Doc/OVERVIEW.md`)
- ❌ No "NASHEED_LIBRARY_FRONTEND.md" (superseded — use `src/Apps/Nasheed/Doc/`)
- ❌ No "GROUPED_CACHE_NAMESPACE_STRATEGY.md" (removed)
- ❌ No "REDIS_ENABLED_VS_DISABLED_GUIDE.md" (content merged into CACHING_STRATEGY_COMPARISON.md)
- ❌ No "JWT_TENANT_VERIFICATION_GUIDE.md" (content in BYPASS_TENANT_ENDPOINTS_GUIDE.md and MULTI_TENANCY_GUIDE.md)

**If you need quick reference info:** It's in the main guide file as a section.

---

## 📊 Documentation Statistics

- **Total Files:** 39 (all in `MicroservicesArchitecture/Doc/`)

**Average file size:** Comprehensive (each file contains complete information on its topic)

---

## 🔄 Maintenance

### Updating This Index

**When to update this file:**

1. ✅ New .md file created → Add entry with description
2. ✅ .md file removed → Remove entry
3. ✅ File purpose changes → Update description
4. ✅ File renamed → Update filename

**How to update:**

1. Keep alphabetical order within each category
2. Keep descriptions concise (1-2 sentences)
3. Keep "Read When" specific and actionable
4. Update "Total Files" count
5. Update "Last Updated" date

### File Count Check

```powershell
# Run this to verify file count matches index
cd Doc
(Get-ChildItem -Filter "*.md").Count
# Should match "Total Files" above
```

---

## ✅ Quick Reference for AI Agents

**Before reading any documentation:**

1. ✅ Am I reading DOCUMENTATION_INDEX.md first? If no, **stop and read this file first**
2. ✅ Do I know which specific files I need? If no, **use Quick Task Lookup table above**
3. ✅ Am I about to read 10+ files? If yes, **you're reading too much, be more specific**

**Remember:**

- 📖 **One file = One topic = Complete information**
- 🚫 **No "quick" files exist** - all content is in the main guide
- ⚡ **Read only what you need** - don't read everything
- 🔄 **Always check this index** - don't assume files exist

---

**Last Updated:** June 6, 2026  
**Maintained By:** AI agents following DOCUMENTATION_GUIDELINES.md
