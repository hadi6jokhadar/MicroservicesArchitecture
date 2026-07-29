# Backup Service Guide

## Overview

The Backup service (`src/Services/Backup/`, port **5010**) is the platform's centralized PostgreSQL backup and restore manager. It replaces manual, one-off `pg_dump` commands with scheduled + on-demand backups across every service's global database and every tenant's database, a dual local/cloud status per backup, and admin endpoints to trigger, inspect, and restore.

Database Strategy: **A — Single Global DB** (see `.claude/instructions/database-strategy.instructions.md`). Backup stores its own operational metadata only — it is not itself multi-tenant and never resolves `ITenantContext`.

---

## Why one dump per tenant is enough

All multi-tenant services (Identity, FileManager, Category, Nasheed, and Notification's tenant-side context) resolve the *same* `ITenantContext.CurrentTenant.Configuration.DatabaseSettings.ConnectionString` for a given tenant, unmodified — see `Doc/DATABASE_PER_TENANT_ARCHITECTURE.md`'s "Project Isolation Revisited" section. One tenant = one physical Postgres database containing all of those services' tables. Backing up that single database captures all of them at once. What Backup additionally tracks separately are each service's own **global/fallback** database (used when there's no tenant context, or by Strategy A/D services like Tenant/Translation/AI which have no per-tenant database at all) — those are configured statically, one entry per service, in Backup's own `appsettings.json`.

---

## Domain model

| Entity | Purpose |
| --- | --- |
| `BackupTargetEntity` | A registered thing to include in scheduled backups. `Scope` = `GlobalService` (has `ServiceName`) or `Tenant` (has `TenantId`). `IsEnabled` / `RetentionDays` are admin-controlled. Tenant targets are upserted automatically (see below) — they never store a connection string, which is always resolved live so it can't go stale. |
| `BackupRunEntity` | One row per backup execution: denormalized `Scope`/`ServiceName`/`TenantId`/`DatabaseName` snapshot, `Status` (Pending/Running/Completed/Failed), and **two independent status fields** — `LocalStatus` (Pending/Saved/Failed/Deleted) and `CloudStatus` (Pending/Uploading/Uploaded/Failed/Disabled). This dual-status pair is the "is it saved locally / is it safely offsite" table view. |
| `RestoreRunEntity` | One row per restore attempt, FK'd to the `BackupRunEntity` it restored from. |

---

## How targets get discovered (seeding)

Two "sync jobs" keep `BackupTargetEntity` populated — both only ever **add** targets, never disable an existing one (an admin's manual disable must stick):

- **`GlobalTargetSyncJob`** — upserts a `BackupTargetEntity` (`Scope=GlobalService`) for every entry in the `Backup:GlobalTargets` config section, keyed by `ServiceName`.
- **`TenantTargetSyncJob`** — calls Tenant Service's `GET /api/v1/tenant/config` (via the shared `ITenantDirectoryClient`, using the existing `AddTenantServiceClient` extension in `IhsanDev.Shared.Infrastructure`, service-to-service auth with `X-Service-Secret`/`X-Service-Name: BackupService`) and upserts a `BackupTargetEntity` (`Scope=Tenant`) for every active tenant that has its own database connection string.

Both run in two places: **once at startup** (`Program.cs`, right after `InitializeDatabaseAsync` — best-effort, wrapped in try/catch so a Tenant Service that isn't up yet doesn't block Backup's own boot) so the admin Overview page is never empty on a fresh deployment, and again at the start of every nightly `BackupSchedulerJob` run so newly-added tenants/config entries get picked up without a restart.

## How a scheduled backup happens

1. `BackupSchedulerJob` runs daily at **01:00 UTC** (Hangfire recurring job, cron `0 1 * * *`).
2. It runs `GlobalTargetSyncJob` then `TenantTargetSyncJob` first (see above).
3. For every `BackupTargetEntity` with `IsEnabled=true`, it enqueues a `RunBackupJob.ExecuteForTargetAsync` Hangfire job.
4. `RunBackupJob` resolves the connection string (`BackupConnectionResolver`: `GlobalService` scope reads `Backup:GlobalTargets` from config by `ServiceName`; `Tenant` scope calls `ITenantDirectoryClient` again and matches by `TenantId`), runs `pg_dump --format=custom` via `IPgToolRunner` to `{Backup:LocalStorageRootPath}/{yyyy-MM-dd}/{scope}_{identifier}_{utc-timestamp}.dump`, and records `LocalStatus`, `FileSizeBytes`, and a SHA-256 `Checksum`.
5. Independently — a cloud-upload failure never flips `LocalStatus` — it uploads to Cloudflare R2 via `IBackupBlobStorage` and records `CloudStatus`. If R2 isn't configured, `CloudStatus=Disabled` (graceful no-op, mirroring FileManager's `NullBlobStorage` pattern via `NullBackupBlobStorage`).

On-demand backups use the same `RunBackupJob` core (`ExecuteAsync`, given an already-created `Pending` run) triggered from `POST /api/v1/admin/backups/trigger`. `TriggerBackupCommandHandler` find-or-creates the `BackupTargetEntity` (`IsEnabled=true`) if one doesn't already exist for the given service/tenant — so triggering a backup for a database that was never seen before both runs it immediately **and** adds it to the nightly schedule going forward, in one action.

## How retention works

`BackupRetentionCleanupJob` runs daily at **03:00 UTC** (cron `0 3 * * *`). It deletes the local file for any run where `LocalStatus=Saved` **and** `CloudStatus=Uploaded` and the run is older than that target's `RetentionDays` override (or `Backup:DefaultRetentionDays`, default 30) — a run whose cloud upload never succeeded is never locally deleted, since its local copy would then be the only copy.

## Restore does not invalidate downstream caches

`pg_restore` only ever touches PostgreSQL. If the service that owns the restored data caches it (e.g. Category service's Redis-cached category tree — see `CATEGORY_SERVICE_GUIDE.md`), that service keeps serving its cached value until the cache entry expires or the service restarts, even though the underlying row was correctly reverted. Verified directly: modify a row → restore the pre-modification backup → query PostgreSQL directly → correct reverted value every time. **After any restore, restart the affected service(s) (or flush their cache) before verifying the result** — a raw `psql` check against the database is the reliable way to confirm a restore actually worked; checking through a long-running service's own read path can show stale data that has nothing to do with the restore itself.

## How a restore happens

`POST /api/v1/admin/backups/{id}/restore` requires an explicit `"confirm": true` in the body (`pg_restore --clean` is destructive — it drops existing objects first) and an optional `targetConnectionOverride` to restore into a different/scratch database instead of overwriting the original. This creates a `RestoreRunEntity` and enqueues `RunRestoreJob`, which downloads the dump from cloud storage first if the local copy was already cleaned up by retention, then runs `pg_restore --clean --if-exists --no-owner`.

---

## Admin API (all routes require the `SuperAdmin` role)

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/v1/admin/backup-targets` | List all registered targets (global + tenant) |
| PATCH | `/api/v1/admin/backup-targets/{id}` | Enable/disable a target or override its retention |
| POST | `/api/v1/admin/backups/trigger` | On-demand backup — `{ scope, serviceName? , tenantId? }` |
| GET | `/api/v1/admin/backups/summary` | **The flagship status table** — one row per known target with its most recent run's local/cloud status, size, and error, if any |
| GET | `/api/v1/admin/backups` | Paginated backup run history, filterable by `scope`/`serviceName`/`tenantId`/`status`/`fromDate`/`toDate` |
| GET | `/api/v1/admin/backups/{id}` | A single backup run's detail |
| POST | `/api/v1/admin/backups/{id}/restore` | Restore — `{ confirm: true, targetConnectionOverride? }` |
| GET | `/api/v1/admin/restores` | Paginated restore run history |

Reachable directly on port 5010, and through the API Gateway at the same paths (added at `Order: 5` in `Gateway.API/appsettings.json`, ahead of Identity's `/api/v1/admin/{**catch-all}` catch-all — the same pattern already used for Tenant's and Category's admin routes).

**`TriggerBackupCommand.Scope` must be sent as a string** (e.g. `{"scope": "GlobalService", "serviceName": "IdentityService"}`), not the underlying `int` — it carries `[JsonConverter(typeof(JsonStringEnumConverter))]` explicitly since no global string-enum converter is registered on this platform (see `.claude/instructions/Dotnet.instructions.md` pitfall #17). Every other enum-shaped field on this API (`Status`, `LocalStatus`, `CloudStatus`, `TriggerType` in responses) is already a plain string because DTOs map them manually via `.ToString()` — `Scope` on the *request* side is the one place a raw enum type is bound from JSON.

The Hangfire dashboard is at `http://localhost:5010/admin/jobs/backup` (HTTP Basic Auth, `Hangfire:Dashboard:Username`/`Password` — see `Doc/HANGFIRE_JOBS_GUIDE.md`), reached directly, never through the gateway (same reasoning as every other service's dashboard: YARP path-rewriting breaks Hangfire's relative dashboard links).

---

## Configuration

`Backup.API/appsettings.json` (placeholders — real values only in the gitignored `appsettings.Development.json`):

```json
"Backup": {
  "PgDumpPath": "",            // empty = look up "pg_dump" on PATH
  "PgRestorePath": "",         // empty = look up "pg_restore" on PATH
  "LocalStorageRootPath": "C:\\Backups\\PostgreSQL",
  "DefaultRetentionDays": 30,
  "GlobalTargets": [
    { "ServiceName": "IdentityService", "DisplayName": "Identity (global)", "ConnectionString": "..." }
    // one entry per service that has its own global/fallback database
  ]
},
"BlobStorage": {
  "Provider": "CloudflareR2",
  "CloudflareR2": { "AccountId": "...", "AccessKeyId": "...", "SecretAccessKey": "...", "BucketName": "microservice-backups", "PublicDomain": "..." }
},
"Services": { "TenantService": { "BaseUrl": "https://localhost:5002", "Timeout": 30 } }
```

**Prerequisite:** the PostgreSQL client tools (`pg_dump`, `pg_restore`) must be installed on the machine running the Backup service, and either on `PATH` or pointed to explicitly via `Backup:PgDumpPath`/`Backup:PgRestorePath` — the same "install a native binary, configure its path, fall back to PATH" convention FileManager already uses for `ffmpeg` (`FileManagerOptions:FfmpegPath`). In local dev, install a client matching the Postgres server's major version. In Docker, this is already handled — `Backup.API/Dockerfile`'s final stage installs `postgresql-client` alongside the usual `libicu-dev` (see `Doc/DOCKER_DEPLOYMENT_GUIDE.md`'s Backup-specific pitfall), and `appsettings.Docker.json` points both paths at `/usr/bin/pg_dump`/`/usr/bin/pg_restore`.

`BlobStorage:CloudflareR2` is a **separate** bucket/credentials from FileManager's — a compromise of one storage credential must never expose the other's data. If left as `CHANGE_ME_*` placeholders, backups still run and save locally; `CloudStatus` just reports `Disabled` instead of `Uploaded`.

### Required cross-service wiring

Tenant Service's `ServiceCommunication:AllowedServices` (in both `appsettings.json` and `appsettings.Development.json`) includes `"BackupService"` — without it, every `ITenantDirectoryClient` call silently 401s (see `.claude/instructions/Dotnet.instructions.md` pitfalls #9/#10).

---

## Frontend

An Angular admin UI exists at `MicroservicesArchitecture-Web/apps/admin/src/app/features/backup/` — see `MicroservicesArchitecture-Web/Doc/BACKUP_FEATURE_GUIDE.md` for the full frontend guide. Summary: **Backup** in the sidebar (System group, SuperAdmin only) →
- **Overview** (`/backup/overview`) — the flagship per-database status table, consumes `GET /api/v1/admin/backups/summary`. A "New Backup" button lets an admin trigger a backup for any service/tenant directly, even ones with no row yet.
- **History** (`/backup/history`) — two tabs: **Backups** (paginated/filterable run log) and **Restores** (paginated restore-attempt log, consumes `GET /api/v1/admin/restores`).

## Troubleshooting

**`pg_dump executable was not found` / `pg_restore executable was not found`** — the PostgreSQL client tools aren't on `PATH` and `Backup:PgDumpPath`/`PgRestorePath` are empty. This is a per-machine local-dev setup issue, not a code bug. Fix: find where they're actually installed (a full PostgreSQL install, e.g. via the EnterpriseDB Windows installer, puts them under `C:\Program Files\PostgreSQL\{version}\bin\`) and set `Backup:PgDumpPath`/`PgRestorePath` in your own gitignored `appsettings.Development.json` to the full `.exe` path, or add that folder to `PATH`. Docker deployments never hit this — `Backup.API/Dockerfile` installs `postgresql-client` and `appsettings.Docker.json` already points both paths at `/usr/bin/pg_dump`/`/usr/bin/pg_restore`.

**A restore reports `Completed` with no error, but the data looks unchanged** — see "Restore does not invalidate downstream caches" above. Verify with a direct `psql` query against the target database, not through a running service's own (possibly cached) read path.

## Known limitations / explicitly out of scope

- **No WAL archiving / point-in-time recovery.** This service only does full `pg_dump`/`pg_restore` snapshots. Continuous WAL archiving remains a `postgresql.conf`-level operational concern (see the original sketch retained in `Doc/PLATFORM_CAPABILITIES_ROADMAP.md` item 9).
- **Global target connection strings are admin-configured, not auto-discovered.** There's no API that exposes another service's own `DatabaseSettings:ConnectionString`, so `Backup:GlobalTargets` has to be filled in by hand once per service.
