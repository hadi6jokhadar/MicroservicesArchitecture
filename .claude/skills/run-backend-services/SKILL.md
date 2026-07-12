---
name: run-backend-services
description: Run, start, launch, smoke-test, or check the health of backend microservices — Identity, Tenant, Notification, FileManager, Translation, Category, AI, Nasheed, Gateway. Use for "start a service", "is the backend running", "run the backend", "launch the services", "test the API".
---

# Run Backend Services

Nine services make up the backend stack. Each has its own process and port. They are launched individually via `run-development-instance.mjs` (or `.bat`) files inside each service's API folder. A smoke-test driver — `smoke.mjs` — lives alongside this skill and provides a programmatic health-probe harness.

All paths below are relative to `MicroservicesArchitecture/` (the .NET backend root).

## Service map

| Name          | Port | Type   | API folder path (from backend root)                          |
|---------------|------|--------|--------------------------------------------------------------|
| gateway       | 5000 | .NET   | `src/Gateway/Gateway.API`                                    |
| identity      | 5001 | .NET   | `src/Services/Identity/Identity.API`                         |
| tenant        | 5002 | .NET   | `src/Services/Tenant/Tenant.API`                             |
| notification  | 5004 | .NET   | `src/Services/Notification/Notification.API`                 |
| filemanager   | 5005 | .NET   | `src/Services/FileManager/FileManager.API`                   |
| translation   | 5006 | .NET   | `src/Services/Translation/Translation.API`                   |
| category      | 5007 | .NET   | `src/Services/Category/Category.API`                         |
| ai            | 5008 | Python | `src/Services/AI/AI.API`                                     |
| nasheed       | 5009 | .NET   | `src/Apps/Nasheed/Nasheed.API`                               |

All services expose `/health` (HTTP GET, 200 = healthy).

## Prerequisites

- **.NET 9 SDK** — `dotnet --version` must show `9.x` (all services target `net9.0`)
- **Node.js 18+** — `node --version` (for the smoke driver and `.mjs` launchers)
- **Python venv** for the AI service — already set up at `src/Services/AI/AI.API/venv/`
- **PostgreSQL** running locally (services connect on startup; no connection = startup failure)
- **Redis** running via Docker Compose (`docker compose -f docker-compose.redis.yml up -d` from the backend root) — Notification and others use it; absence causes runtime errors, not startup crash

## Agent path — smoke driver

The smoke driver at `.claude/skills/run-backend-services/smoke.mjs` is the primary tool for agents. Run it from anywhere — it uses absolute paths internally.

### Check which services are currently running (probe only)

```powershell
node ".claude/skills/run-backend-services/smoke.mjs"
```

Output: one line per service showing UP/DOWN and HTTP status code. Exits 0 if all probed services are UP, 1 if any are DOWN.

### Start one or more services, then probe all

```powershell
node ".claude/skills/run-backend-services/smoke.mjs" identity tenant
```

This starts `identity` and `tenant` in-process (output prefixed with `[identity]` / `[tenant]`), waits 15 s for startup, probes all nine health endpoints, then kills the started children. Use this to verify a change didn't break startup.

### Start services and probe only those services

```powershell
node ".claude/skills/run-backend-services/smoke.mjs" --only identity tenant
```

Same as above but only probes the named services instead of all nine.

### Valid service names

```
identity  tenant  notification  filemanager  translation  category  nasheed  gateway
```

**`ai` is listed in the map but start-via-driver is not recommended** — the Python venv activation is fragile inside the Node subprocess chain. Start AI manually (see below).

## Start a single service (long-running, foreground)

Run from the service's own API folder. The `.mjs` launchers are cross-platform:

```powershell
# .NET services (example: Identity on port 5001)
cd "MicroservicesArchitecture/src/Services/Identity/Identity.API"
node run-development-instance.mjs

# AI service (Python FastAPI on port 5008)
cd "MicroservicesArchitecture/src/Services/AI/AI.API"
venv\Scripts\activate.bat
uvicorn main:app --reload --port 5008
```

These block the terminal until stopped with Ctrl-C.

## Human path — start all services at once

The Windows-only all-in-one launcher opens one Windows Terminal tab per service:

```powershell
cd "MicroservicesArchitecture/src/Services"
node start-all-services.mjs
```

This calls `wt.exe` and is useless in headless or non-Windows Terminal environments. Use the agent path above for programmatic use.

## Run all tests

```powershell
cd "MicroservicesArchitecture/src/Services"
dotnet test ..\Services\Identity\Identity.API.Tests\Identity.API.Tests.csproj
dotnet test ..\Services\Tenant\Tenant.API.Tests\Tenant.API.Tests.csproj
dotnet test ..\Services\Notification\Notification.API.Tests\Notification.API.Tests.csproj
dotnet test ..\Services\FileManager\FileManager.API.Tests\FileManager.API.Tests.csproj
dotnet test ..\Services\Translation\Translation.API.Tests\Translation.API.Tests.csproj
```

Or use the batch wrapper (opens separate WT tabs — human-only):

```powershell
cd "MicroservicesArchitecture/src/Services"
.\run-all-tests.bat
```

## Gotchas

- **`&` is reserved in PowerShell** — never chain commands with `&`. Use `;` or run sequentially.
- **`start-all-services.mjs` requires `wt.exe`** — it does nothing useful without Windows Terminal. For agents, use `smoke.mjs` or `run-development-instance.mjs` directly.
- **AI service venv path** — must activate `venv\Scripts\activate.bat` before running `uvicorn`, or call the venv Python directly: `venv\Scripts\python.exe -m uvicorn main:app --reload --port 5008`. The `.mjs` launcher handles this automatically.
- **Startup time** — .NET services take 5–10 s to reach `/health`. The smoke driver waits 15 s; for a slow machine with cold JIT, increase to 20–25 s if you see false DOWN results on first run.
- **Database required at startup** — if PostgreSQL is not running, .NET services exit immediately with a connection error. The smoke driver will show DOWN + "connection refused" on the health port (not a startup error message). Start Postgres first.
- **Port conflicts** — if another process holds a port, `dotnet run` fails silently from the smoke driver's perspective. Check with `netstat -ano | findstr :5001` if a service shows DOWN despite appearing to start.
- **Nasheed port** — the Notification `.bat` file shows `5004` in its header but sets `ASPNETCORE_URLS=http://localhost:5004;https://localhost:5104`. The Nasheed service is on `5009`, not `5004`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `wt.exe: command not found` | You're running outside Windows Terminal. Use `run-development-instance.mjs` instead. |
| Service shows DOWN 15 s after start | PostgreSQL not running; or cold dotnet restore on first run (try 30 s delay). |
| AI service crashes on `import` | Venv not activated / dependencies missing. Run `pip install -r requirements.txt` inside the venv. |
| `dotnet run` fails with SDK error | Run from inside the `.API` folder, not the solution root. |
| Health endpoint returns 503 | Service started but a health check dependency (DB, Redis) is unhealthy. Check logs. |
