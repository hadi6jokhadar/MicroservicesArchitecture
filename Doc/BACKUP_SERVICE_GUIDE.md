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
| GET | `/api/v1/admin/restores` | Paginated restore run history — `RestoreRunDto.targetOverrideSummary` (host/database only, e.g. `"scratch-host/scratch_db"`), never the raw override string or its password (see "Security notes" below) |

Reachable directly on port 5010, and through the API Gateway at the same paths (added at `Order: 5` in `Gateway.API/appsettings.json`, ahead of Identity's Order-20 admin routes — the same pattern already used for Tenant's and Category's admin routes). Identity's admin routes are scoped to its actual prefixes (`/api/v1/admin/users`, `/roles`, `/claims` — see `API_GATEWAY_GUIDE.md`), not a bare `/api/v1/admin/{**catch-all}`, as of the July 2026 security audit.

**`TriggerBackupCommand.Scope` must be sent as a string** (e.g. `{"scope": "GlobalService", "serviceName": "IdentityService"}`), not the underlying `int` — it carries `[JsonConverter(typeof(JsonStringEnumConverter))]` explicitly since no global string-enum converter is registered on this platform (see `.claude/instructions/Dotnet.instructions.md` pitfall #17). Every other enum-shaped field on this API (`Status`, `LocalStatus`, `CloudStatus`, `TriggerType` in responses) is already a plain string because DTOs map them manually via `.ToString()` — `Scope` on the *request* side is the one place a raw enum type is bound from JSON.

The Hangfire dashboard is at `http://localhost:5010/admin/jobs/backup` (HTTP Basic Auth, `Hangfire:Dashboard:Username`/`Password` — see `Doc/HANGFIRE_JOBS_GUIDE.md`), reached directly, never through the gateway (same reasoning as every other service's dashboard: YARP path-rewriting breaks Hangfire's relative dashboard links). `HangfireBasicAuthFilter` compares credentials via `CryptographicOperations.FixedTimeEquals` over SHA-256 digests (not plain `==`) and throttles a per-IP failed-attempt window (5 failures / 5 minutes → `429`) — see "Security notes" below.

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

**`The 'pg_dump'/'pg_restore' executable was not found...`** (`LocalizationKeys.Exceptions.BackupToolNotFound`) — the PostgreSQL client tools aren't on `PATH` and `Backup:PgDumpPath`/`PgRestorePath` are empty. This is a per-machine local-dev setup issue, not a code bug. Fix: find where they're actually installed (a full PostgreSQL install, e.g. via the EnterpriseDB Windows installer, puts them under `C:\Program Files\PostgreSQL\{version}\bin\`) and set `Backup:PgDumpPath`/`PgRestorePath` in your own gitignored `appsettings.Development.json` to the full `.exe` path, or add that folder to `PATH`. Docker deployments never hit this — `Backup.API/Dockerfile` installs `postgresql-client` and `appsettings.Docker.json` already points both paths at `/usr/bin/pg_dump`/`/usr/bin/pg_restore`.

**A backup/restore run shows `LocalStatus: Failed` with a message like `pg_dump failed for database 'X' (exit code 1): <stderr first line>`** (`LocalizationKeys.Exceptions.BackupProcessFailedWithDetails`, thrown by `PgToolRunner.RunProcessAsync` and localized via `ILocalizationService` before being stored verbatim as `BackupRunEntity.ErrorMessage`/`RestoreRunEntity.ErrorMessage`) — `pg_dump`/`pg_restore` started but exited non-zero; the trailing stderr snippet is the actual root cause (e.g. `FATAL: database "X" does not exist`, `FATAL: password authentication failed for user "Y"`, `could not connect to server: Connection refused`). Read that snippet first instead of re-deriving the cause from scratch. The exit-timestamp gap in the Backup Run Details view is also diagnostic: a failure within ~1 second of `StartedAt` means the connection/database check failed immediately (auth, missing DB, unreachable host), while a failure after tens of seconds/minutes means the dump ran partway then hit something mid-stream (disk space, permissions on the output path, a lock).
  - **If the root cause is "database does not exist" for a `GlobalService`-scope target:** check whether that service actually maintains a real, always-present global database at runtime, not just one used once for `dotnet ef migrations add` tooling. `Backup:GlobalTargets` entries must point at a database the target service's own `Program.cs`/`DbContext` actually creates/migrates on startup (Strategy A/D services, or a Strategy B service's documented global fallback DB per `.claude/instructions/database-strategy.instructions.md`) — not a single-tenant Strategy B service (like Nasheed) whose only real data lives in a per-tenant database resolved via Tenant Service. Adding a `GlobalTargets` entry by copy-pasting another service's pattern without confirming this is how `NasheedService`'s `nasheed_dev` target started failing every run (July 2026) — its own `Nasheed.API/Program.cs` explicitly documents "Nasheed has no global database."

**A restore reports `Completed` with no error, but the data looks unchanged** — see "Restore does not invalidate downstream caches" above. Verify with a direct `psql` query against the target database, not through a running service's own (possibly cached) read path.

**`pg_restore: error: unsupported version (X.YY) in file header` when restoring a PC1 dev-machine backup into PC2's Docker Postgres** — the custom-format archive's internal version number reflects the `pg_dump` **client** version that created it, not the server version. PC1's local dev machine can easily have a newer PostgreSQL client installed (e.g. 18.x, installed standalone for local dev) than `docker-compose.yml`'s `postgres:15-alpine` image ships (server 15.18) or even than `Backup.API`'s own container's `postgresql-client` package (16.x, per the Dockerfile pitfall above) — and `pg_restore` refuses to read an archive version newer than itself, even though the actual target *server* version is often perfectly capable of receiving the restored data once the archive is decoded by a new-enough client. Found migrating dev backups from PC1 (pg_dump 18.3) to PC2 in August 2026 — PC2's postgres container's own `pg_restore` (15.18) and even the `backup` container's `pg_restore` (16.14) both rejected the archives. **Fixed** by using PC1's own (newer) `pg_restore.exe` directly, tunneled to PC2's Postgres over SSH (`ssh -N -L 15432:localhost:5432 <PC2_SSH_HOST>`, since `docker-compose.yml` binds Postgres to `127.0.0.1` on PC2 only) — `pg_restore.exe --clean --if-exists --no-owner -h localhost -p 15432 -U postgres -d <database> <dump file>` restored cleanly with only a harmless warning (see below). **When restoring across machines with different Postgres client versions, restore FROM whichever machine has the newest client**, tunneling to the target server rather than copying the file to an older-client machine and restoring locally there.

**`pg_restore: error: could not execute query: ERROR: unrecognized configuration parameter "transaction_timeout"`** — a single, harmless warning (`errors ignored on restore: 1`) when restoring an archive made by pg_dump 17+ into a Postgres 15 (or older) server; `transaction_timeout` is a session-level GUC added in newer PostgreSQL that the older server doesn't recognize. It's one of several session-normalization `SET` statements pg_dump emits at the start of the restore, not a schema/data statement — the actual restore proceeds normally. Safe to ignore.

**`pg_restore: error: could not execute query: ERROR: extension "vector" is not available`** — the source database had the `pgvector` extension installed (e.g. AI service's database, for embedding columns) but the target Postgres image (`postgres:15-alpine`, the plain upstream image used by `docker-compose.yml`) doesn't have `pgvector` built in. `pg_restore` logs this as an ignored error and continues — check afterward (`\dt` in `psql`) whether any *tables* actually failed to materialize as a result (a table using a `vector`-typed column would fail to create without the extension) — if the affected service doesn't currently have any real vector-typed columns yet, this is a no-op warning with nothing actually missing. If real vector columns exist, the fix is switching the `postgres` service in `docker-compose.yml` to the `pgvector/pgvector:pg15` image (drop-in compatible, adds the extension) instead of plain `postgres:15-alpine`.

## Manual cross-machine restore: PC1 dev backups → PC2 Docker

This bypasses the Backup service's own REST API entirely — there's no `BackupRunEntity` row for a PC1 dev-machine dump, so it can't be restored via `POST /api/v1/admin/backups/{id}/restore` (that endpoint only knows about runs it created itself). Use this runbook when you need to seed/refresh PC2 with PC1's local dev data directly via `pg_restore`. Used successfully in August 2026 to migrate 11 databases (7 global + 4 tenant) from PC1 to PC2.

**Before starting:** this is destructive (`pg_restore --clean` drops existing objects in the target database first) and requires downtime for every service whose database you're restoring. Confirm with whoever owns PC2 that overwriting its current data is actually wanted, and confirm exactly which backup date/set to use — PC1 accumulates one dated folder per day under `Backup:LocalStorageRootPath` (`C:\Backups\PostgreSQL\{yyyy-MM-dd}\`), and the newest folder isn't always the most *complete* one (a partial/interrupted backup run can leave a newer folder with fewer files than the day before).

### Prerequisites

- SSH from PC1 to PC2 already working passwordless (`ssh <PC2_SSH_HOST> "echo connected"`) — see `Doc/DOCKER_DEPLOYMENT_GUIDE.md`'s SSH setup section if not.
- Know PC1's local `pg_dump`/`pg_restore` client version (`"C:\Program Files\PostgreSQL\{version}\bin\pg_restore.exe" --version`) — you'll very likely need to restore *from* PC1 using this client rather than any tool living on PC2, per the version-mismatch pitfall above.

### Steps

1. **Identify the backup files to migrate.** List PC1's backup folder for the date you want:
   ```powershell
   Get-ChildItem -Path "C:\Backups\PostgreSQL\{yyyy-MM-dd}" -File | Select-Object Name, Length, LastWriteTime
   ```
   Pick the single latest timestamp-group that has a complete set (one file per service/tenant you care about) — `RunBackupJob`'s filenames are `{scope}_{identifier}_{yyyyMMddHHmmss}.dump`, so files from the same actual backup run share nearly-identical timestamps (seconds apart).

2. **Map each filename to its target database name.** `globalservice_{ServiceName}_*.dump` → look up `{ServiceName}` in `Backup.API/appsettings.Docker.json`'s `Backup:GlobalTargets` list for the `ConnectionString`'s `Database=` value (it is **not** always the obvious lowercase service name — e.g. `NasheedService`'s global target is configured to point at the `anashid` tenant database, not a `nasheed`-named one). `tenant_{tenantId}_*.dump` → the database is simply `{tenantId}` (one tenant = one database, named after the tenant ID).

3. **Check which target databases already exist on PC2**, and create any that don't (a brand-new tenant that's never been accessed on PC2 has no database yet — `pg_restore` doesn't create the database itself unless invoked with `--create`, and creating it manually first is simpler):
   ```bash
   ssh <PC2_SSH_HOST> "/usr/local/bin/docker exec ihsandev-postgres psql -U postgres -l"
   ssh <PC2_SSH_HOST> "/usr/local/bin/docker exec ihsandev-postgres psql -U postgres -c 'CREATE DATABASE {name};'"   # repeat per missing DB
   ```

4. **Stop every PC2 app container whose database you're about to overwrite** (avoids connection locks blocking `--clean`'s drops, and their in-memory caches need clearing afterward anyway):
   ```bash
   ssh <PC2_SSH_HOST> "cd <PC2_REPO_PATH> && /usr/local/bin/docker compose stop identity tenant notification filemanager translation category ai nasheed"
   ```
   (list only the services actually affected by your restore set)

5. **Open an SSH tunnel from PC1 to PC2's Postgres** (bound to `127.0.0.1` on PC2, per `docker-compose.yml` — not reachable directly from PC1 otherwise):
   ```bash
   ssh -N -L 15432:localhost:5432 <PC2_SSH_HOST>
   ```
   Run this in the background (or a separate terminal) — it needs to stay open through step 6.

6. **Restore each file from PC1, through the tunnel, using PC1's own (newer) client:**
   ```powershell
   $env:PGPASSWORD = "<POSTGRES_PASSWORD from .env>"
   & "C:\Program Files\PostgreSQL\{version}\bin\pg_restore.exe" --clean --if-exists --no-owner `
     -h localhost -p 15432 -U postgres -d {targetDatabase} `
     "C:\Backups\PostgreSQL\{yyyy-MM-dd}\{filename}.dump"
   ```
   Repeat per file/database. Expect (and ignore) the `transaction_timeout`/`pgvector` warnings described above if they apply — anything else in the output is a real problem worth investigating before moving on to the next file.

7. **Close the SSH tunnel** (`Ctrl+C` in its terminal, or find and kill the `ssh.exe` process by its command line if it's running detached).

8. **Restart the app containers** — this project's `docker/deploy-pc2.mjs` script (`node docker/deploy-pc2.mjs <service...>`) does a `pull` + `up -d`, which also works fine here even though there's no new image to pull (it just starts the stopped containers):
   ```bash
   node docker/deploy-pc2.mjs identity tenant notification filemanager translation category ai nasheed
   ```

9. **Verify** — container status/restart counts, `/health` endpoints, and a direct `psql` row-count spot-check per restored database (not through a service's own read path, which may be stale-cached — see "Restore does not invalidate downstream caches" above):
   ```bash
   ssh <PC2_SSH_HOST> "/usr/local/bin/docker inspect -f '{{.State.Status}} restarts={{.RestartCount}}' identity"
   ```
   ```powershell
   $env:PGPASSWORD = "..."; & "...\psql.exe" -h localhost -p 15432 -U postgres -d identity -c "SELECT count(*) FROM \"Users\";"
   ```
   (re-open the SSH tunnel from step 5 first if you closed it and still want to `psql` from PC1 — or just run the `psql` check via `docker exec` on PC2 instead, using whichever container has a new-enough client per the version-mismatch pitfall)

10. **Migrate FileManager's local file storage too, or the restored database's file references will 404.** The database restore only brings back *rows* — `FileManagerEntity.Path` is a relative path (e.g. `ihsandev/system/image/{uuid}.webp`) resolved against `FileManagerOptions.RootStoragePath` (`C:\FileStorage` in PC1 local dev, the `ihsandev_filestorage` named volume mounted at `/app/FileStorage` in PC2's `filemanager` container per `docker-compose.yml`). If a file was never uploaded to Cloudflare R2 (check the row's `ExternalUrl` column — if populated, FileManager serves from R2 regardless of local disk and this step doesn't matter for that specific file), the actual bytes only exist on whichever machine's disk they were originally saved to. Restoring the database without also copying these files leaves every un-migrated local file's URL pointing at nothing on PC2.

    Since `/app/FileStorage` is a **named volume** (not a bind mount), files can't be dropped in with a plain host-to-host file copy — `docker cp` into the running container is what actually writes through to the volume. No downtime is needed here (unlike the database restore) — this is purely additive, and the `filemanager` service doesn't need to be stopped to receive new files on disk:

    ```powershell
    # From PC1 — sanity-check what you're about to move
    Get-ChildItem -Path "C:\FileStorage" -Recurse -File | Measure-Object -Property Length -Sum
    ```
    ```bash
    # Check PC2 isn't about to silently merge onto files it already has with the same names
    ssh <PC2_SSH_HOST> "/usr/local/bin/docker exec filemanager sh -c 'find /app/FileStorage -type f | wc -l'"

    # Transfer: PC1 -> PC2 host temp dir -> into the container's volume-backed path
    ssh <PC2_SSH_HOST> "mkdir -p /tmp/filestorage-inbox"
    ```
    ```powershell
    scp -r "C:\FileStorage\." <PC2_SSH_HOST>:/tmp/filestorage-inbox/
    ```
    ```bash
    ssh <PC2_SSH_HOST> "/usr/local/bin/docker cp /tmp/filestorage-inbox/. filemanager:/app/FileStorage/ && rm -rf /tmp/filestorage-inbox"

    # Verify count/size landed correctly
    ssh <PC2_SSH_HOST> "/usr/local/bin/docker exec filemanager sh -c 'find /app/FileStorage -type f | wc -l; du -sh /app/FileStorage'"
    ```

    **Confirm the link actually works** — don't just trust that "file count matches" means every path resolves. Pick a real `Path` value out of the just-restored database (both a tenant DB and the global `filemanager` DB, since both have their own `FileManager` table — Strategy B means each tenant's own database carries its own file rows) and verify that exact relative path exists on disk:
    ```powershell
    $env:PGPASSWORD = "..."; & "...\psql.exe" -h localhost -p 15432 -U postgres -d ihsandev -c "SELECT \"Path\" FROM \"FileManager\" LIMIT 5;"
    ```
    ```bash
    ssh <PC2_SSH_HOST> "/usr/local/bin/docker exec filemanager sh -c 'find /app/FileStorage -iname \"{uuid-from-above}*\"'"
    ```
    A tenant that never uploaded any files (e.g. a brand-new one with no `FileManager` rows at all) simply has no subfolder to migrate — that's expected, not a gap.

## Security notes (July 2026 audit)

- **`IPgToolRunner` builds `pg_dump`/`pg_restore` arguments via `ProcessStartInfo.ArgumentList`**, not a formatted `Arguments` string — each token (`-h`, the host value, `-p`, the port value, etc.) is passed individually, so a malicious host/username/database value can't break out into a second shell-parsed argument. The password never touches arguments at all — it's set via the `PGPASSWORD` environment variable on the child process, same as before.
- **`TriggerRestoreCommandValidator` allow-lists `targetConnectionOverride`'s connection-forming components** (host/server, port, username, database — not the password) against `^[A-Za-z0-9._-]+$` before the command reaches `PgToolRunner`, as defense in depth on top of `ArgumentList` (which already neutralizes shell injection on its own).
- **`RestoreRunDto` never serializes the raw `TargetConnectionOverride`.** `RestoreRunEntity.TargetConnectionOverride` (which typically embeds `Password=...`) is still persisted verbatim in the DB — `RunRestoreJob` reads it back from the entity directly at execution time, so the alternate-target restore feature still works — but `GetRestoreRunsQueryHandler`'s `GET /api/v1/admin/restores` and `TriggerRestoreCommandHandler`'s own response both go through `RestoreRunDto.MapFrom`, which maps it to `TargetOverrideSummary` (`"{host}/{database}"`, or `null` when no override was used) instead. Matches the standard the statically-configured `Backup:GlobalTargets` connection strings already follow — never serialize a secret-bearing connection string over HTTP.

## Known limitations / explicitly out of scope

- **No WAL archiving / point-in-time recovery.** This service only does full `pg_dump`/`pg_restore` snapshots. Continuous WAL archiving remains a `postgresql.conf`-level operational concern (see the original sketch retained in `Doc/PLATFORM_CAPABILITIES_ROADMAP.md` item 9).
- **Global target connection strings are admin-configured, not auto-discovered.** There's no API that exposes another service's own `DatabaseSettings:ConnectionString`, so `Backup:GlobalTargets` has to be filled in by hand once per service.
