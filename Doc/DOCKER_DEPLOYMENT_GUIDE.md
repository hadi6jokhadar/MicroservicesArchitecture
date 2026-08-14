# Docker Deployment Guide

**Description:** How to build, push, and run the full stack (10 backend services + 3 frontend apps) as Docker containers, using Docker Hub as the sync point between a Windows dev machine (PC1) and a Mac host machine (PC2).

---

## Quick Start: Deploying On a Fresh Server Pair

Follow this in order for a brand-new PC1/PC2 pair (or re-read it as a checklist if something's not working — most failures so far were silent, so re-verifying each step explicitly is worth it). "PC1" = the Windows build machine; "PC2" = the Mac host machine (the same pattern works for a Linux PC2 with minor path/package-manager adjustments, but everything below is written for macOS since that's what's actually been run).

### 1. Set up SSH from PC1 to PC2 first — everything else depends on it

The whole rest of this guide (deploy scripts, file transfers, remote verification) assumes you can already run `ssh <PC2_SSH_HOST>` from PC1 with no password prompt. Set this up before anything else:

1. On PC1, generate a key if you don't already have one: `ssh-keygen -t ed25519` (accept the default path, `~/.ssh/id_ed25519`).
2. On PC2, enable **Remote Login**: System Settings → General → Sharing → toggle "Remote Login" on. Note the username shown there (e.g. `hady`) and PC2's LAN IP (shown in the same panel, or `ipconfig getifaddr en0` in PC2's own terminal).
3. Copy your public key to PC2 — from PC1: `ssh-copy-id <username>@<PC2-LAN-IP>` (or manually append `~/.ssh/id_ed25519.pub`'s contents to `~/.ssh/authorized_keys` on PC2 if `ssh-copy-id` isn't available on Windows).
4. Add a `Host` entry to PC1's `~/.ssh/config` (create the file if it doesn't exist) so every later command in this guide can just say `ssh <PC2_SSH_HOST>`:
   ```
   Host 192.168.1.45
     HostName 192.168.1.45
     IdentityFile "C:\Users\<you>\.ssh\id_ed25519"
     User hady
   ```
   The `Host` value can be a friendly alias instead of the raw IP (e.g. `Host pc2`) — either works, since it's just a lookup key in this config file. **If PC2's IP ever changes** (DHCP lease renewal, router replacement), update this entry — see step 5 below for making PC2's IP stable in the first place.
5. Verify: `ssh 192.168.1.45 "echo connected"` (or whatever alias you chose) should print `connected` with no password prompt.
6. **Give PC2 a static/reserved LAN IP** (DHCP reservation on the router) — a `~/.ssh/config` entry (and every deploy script's `PC2_SSH_HOST`) pointing at a Wi-Fi-assigned IP will silently break if the lease changes.

### 2. One-time setup on PC1 (the build machine)

1. Install Docker Desktop. Confirm multi-platform build support: `docker buildx ls` should list `linux/amd64` and `linux/arm64` under available platforms (Docker Desktop bundles QEMU for this automatically).
2. Clone **both** repos as sibling folders under the same parent directory (`docker-compose.yml` assumes this layout via `../MicroservicesArchitecture-Web`):
   ```
   SomeParentFolder/
     MicroservicesArchitecture/          (backend — this repo)
     MicroservicesArchitecture-Web/      (frontend)
   ```
3. In the backend repo root: copy `.env.example` → `.env` and fill in **every** value — see the table below. `docker login` with your Docker Hub account afterward.

   | Variable | What to set it to |
   |---|---|
   | `DOCKERHUB_USERNAME` | Your Docker Hub account — must match whatever account you `docker login` with (a mismatch here fails every push with "requested access to the resource is denied"; verify with `cmdkey /list \| findstr docker` or just try a push) |
   | `POSTGRES_PASSWORD`, `POSTGRES_REPLICATION_PASSWORD` | Generate real random values — these become the actual Postgres credentials |
   | `REDIS_PASSWORD` | Generate a real random value — used by both `docker-compose.yml`'s `redis` container and the host-dev `docker-compose.redis.yml` |
   | `PGADMIN_EMAIL`, `PGADMIN_PASSWORD`, `GRAFANA_ADMIN_USER`, `GRAFANA_ADMIN_PASSWORD` | Whatever you want to log into those dashboards with (both bound to `127.0.0.1` on PC2 only, not internet-facing) |
   | `CORS_EXTRA_ORIGINS` | Every real frontend origin for **this** server, comma-separated, no spaces — see step 6 below (you need the hostname decided first) |
   | `PC2_SSH_HOST` | The same value you put in `~/.ssh/config`'s `Host` line in step 1 |
   | `PC2_REPO_PATH` | The absolute path where you'll clone the backend repo on PC2 in step 3, e.g. `/Users/hady/Desktop/MicroservicesArchitecture/MicroservicesArchitecture` |

4. **If you want internet access, decide the hostname now, before building anything** — see step 6 below. `environment.docker.ts` (frontend) bakes the hostname in at build time; changing it later means a rebuild.

### 3. One-time setup on PC2 (the host machine)

1. Install Docker Desktop.
2. Clone **only the backend repo**, at the exact path you put in `.env`'s `PC2_REPO_PATH` — PC2 never builds anything, only pulls pre-built images, so the frontend repo isn't needed there at all.
3. **Transfer the gitignored files from PC1** (scp over the SSH connection from step 1 — never `git pull`, since these are intentionally excluded from git and always will be):
   ```powershell
   # From PC1, backend repo root:
   scp .env <PC2_SSH_HOST>:<PC2_REPO_PATH>/.env
   # Repeat per service for every appsettings.Docker.json — see the list below
   scp "src/Services/Identity/Identity.API/appsettings.Docker.json" <PC2_SSH_HOST>:<PC2_REPO_PATH>/src/Services/Identity/Identity.API/
   ```
   - `.env` (same one from PC1, or PC2-specific if credentials differ)
   - Every `appsettings.Docker.json` — one per backend service + Gateway (10 files total: Identity, Tenant, Notification, FileManager, Translation, Category, AI, Nasheed, Backup, Gateway)
   - Verify all 10 landed: `ssh <PC2_SSH_HOST> "find <PC2_REPO_PATH> -name appsettings.Docker.json | wc -l"` should print `10`. Missing one means that service silently runs on the base `appsettings.json`'s placeholder defaults with no startup error (see the AI bind-mount pitfall below for exactly this failure mode).
   - **These files drift out of sync easily** — since they're gitignored, nothing ever re-syncs them automatically. If you edit one of these files later (on PC1, to fix a bug or add a new backend hostname), you must `scp` it to PC2 again yourself; a running container never sees the edit until you do. This exact class of bug (PC1's copy is correct, PC2 is still running an old/incomplete one) has been the single most common failure mode in this deployment — when a service's runtime behavior doesn't match what's in the repo, suspect this before suspecting a code bug.
4. `docker login` (optional — raises the free-tier pull-rate limit above the ~100/hr anonymous ceiling, worth doing since this stack pulls ~17 images). Do this from PC2's own Terminal.app, not over SSH — a non-interactive SSH session can't unlock the macOS Keychain that Docker's credential helper needs (`keychain cannot be accessed because the current session does not allow user interaction`). The deploy scripts in step 5 below work around this automatically for `pull`, so this step is only needed if you also want to `docker login` for other reasons.
5. **macOS-specific:** disable **AirPlay Receiver** (System Settings → General → AirDrop & Handoff) — it squats on port 5000, which Gateway needs. Without this, `docker compose up -d` fails with `address already in use` on port 5000.

### 4. Build and push (from PC1)

Run these from `MicroservicesArchitecture-Web` (the Nx workspace root — the `docker` project's `project.json` lives there even though most of what it builds is the backend repo's images). **Running `nx run docker:...` from the backend repo fails with "no Nx workspace found"** — this is a common mistake.

```powershell
nx run docker:build-push-all      # first deploy — builds and pushes all 13 images
```

First-time builds take a while — the AI image alone can take 20+ minutes for its arm64 variant (PyTorch install). Routine rebuilds are much faster since Docker's layer cache skips anything unchanged — see "Routine deploys" below for the day-to-day flow.

### 5. Deploy to PC2 (from PC1 — no manual SSH needed)

```powershell
nx run docker:deploy-all      # pulls + recreates every service on PC2, using PC2_SSH_HOST/PC2_REPO_PATH from .env
```

This SSHes into PC2 for you and handles the macOS Docker Hub credential workaround automatically (see "SSH + Docker Hub credential workaround" below for what it's doing under the hood, and for the manual fallback if `PC2_SSH_HOST`/`PC2_REPO_PATH` aren't set up yet). If you'd rather run it manually on PC2 directly:

```bash
docker compose pull
docker compose up -d
```

### 6. Verify — `docker compose ps` showing `Up` is NOT proof it works

Several real bugs found during this deployment (ICU crash-loop, hardcoded `localhost` binding, stale PC2 config files) all showed a completely normal `Up` status and unremarkable CPU in `docker compose ps`/`docker stats`, while being **entirely non-functional**. Always follow up with an actual request — either run these on PC2 directly, or from PC1 via `ssh <PC2_SSH_HOST> "<command>"`:

```bash
docker stats --no-stream                                            # CPU pinned near 100% on a .NET service = something's wrong even if status says Up
docker inspect -f 'status={{.State.Status}} restarts={{.RestartCount}}' <service>   # a climbing restart count = crash-looping even if currently "running"
curl -sS -o /dev/null -w 'HTTP %{http_code}\n' http://localhost:5000/health   # repeat per service port
docker logs <service> --tail 30                                     # check for anything after "Application started"
```

If the frontend is reachable but a browser page shows a CORS error in the console, `curl` alone won't catch it (curl never sends an `Origin` header) — verify with a real preflight request instead:

```bash
curl -sS -i -X OPTIONS 'http://localhost:5000/api/v1/<some-route>' -H 'Origin: http://<your-frontend-origin>' -H 'Access-Control-Request-Method: GET'
# Look for: HTTP/1.1 204 No Content  and  Access-Control-Allow-Origin: http://<your-frontend-origin>
```

### 7. Routine deploys (after the first one)

Day to day, you don't need `build-push-all`/`deploy-all` — use the change-aware, one-command version instead:

```powershell
nx run docker:build-deploy-changed   # detects what changed via git diff, builds+pushes it, deploys it to PC2 — all in one command
```

`build-deploy-changed` detects changes from **uncommitted changes first, falling back to the single last commit** if nothing's uncommitted — it does NOT look back across multiple already-committed-but-never-deployed commits. If you're not sure everything committed since the last deploy actually made it to PC2 (e.g. you made several commits before remembering to deploy), fall back to `nx run docker:build-push-all` + `nx run docker:deploy-all` once to get back to a known-good baseline, then resume routine `build-deploy-changed` deploys from there. Other targets, if you want more control:

```powershell
nx run docker:build-changed        # build+push only — review before deploying
nx run docker:deploy-changed       # re-detects the same changed set and deploys it (only meaningful right after build-changed, before anything else changes on disk)
nx run docker:build-identity       # build+push one specific image by name — see tools/docker/project.json for the full list
nx run docker:deploy-all           # deploy every service regardless of what changed
```

### 8. Expose to the internet (optional)

1. Get a hostname pointed at PC2's public IP — either a real domain, or a free DDNS service (this deployment uses one) if you don't have a domain yet.
2. **Before building**, set that hostname in `environment.docker.ts` for `admin` and `nasheed-admin` (`MicroservicesArchitecture-Web/apps/{admin,nasheed/admin}/src/environments/environment.docker.ts`) — it's baked into the compiled JS at build time.
3. Set the same hostname (with every real frontend origin/port) in `.env`'s `CORS_EXTRA_ORIGINS` — see the Cors pitfall below for the exact format and which services need a rebuild vs. just a restart to pick it up.
4. Forward these ports on your router to PC2's static LAN IP: **4200, 4300, 4301, 5000** — as of the July 2026 security audit, the 9 backend service ports (5001–5010, excluding Gateway's 5000) are bound to `127.0.0.1` on the host (see "Known limitations" below), so forwarding them no longer does anything; only Gateway, admin, nasheed-admin, and nasheed-web are reachable from outside PC2. (Frontend host ports were changed from 80/8081/8082 to 4200/4300/4301 in August 2026 to match local dev ports — see the note under "Known limitations" below.)
5. Test from **outside** your local network (e.g. phone on cellular data, not the same Wi-Fi) — a LAN-only test won't catch a missing port-forward rule.

### 9. Connecting to PC2's Postgres/Redis directly from PC1 (e.g. with pgAdmin/DBeaver)

Postgres (`5432`) and Redis (`6379`) are bound to `127.0.0.1` on PC2 only — `<PC2-LAN-IP>:5432` will just refuse the connection, by design (same July 2026 hardening as every other backend port). To reach them from PC1, tunnel through the SSH connection from step 1:

```powershell
ssh -L 5432:localhost:5432 <PC2_SSH_HOST>
```

Keep that terminal open, then point your Postgres client at `localhost:5432` on PC1 (credentials: `postgres` / the real `POSTGRES_PASSWORD` from `.env`, not the tracked placeholder). If PC1 already has its own local Postgres on port 5432, forward to a different local port instead: `ssh -L 15432:localhost:5432 <PC2_SSH_HOST>`, then connect to `localhost:15432`. Same pattern works for Redis (`-L 6379:localhost:6379`) or any other `127.0.0.1`-bound port on PC2.

### SSH + Docker Hub credential workaround (macOS)

The `deploy-*` Nx targets (`docker/pc2-deploy-lib.mjs`) already do this automatically — you only need it if running `docker compose pull`/`build`/`push` manually over SSH yourself and hitting the Keychain error from step 3.4 above:

```bash
mkdir -p /tmp/docker-nocreds/cli-plugins
ln -sf /Applications/Docker.app/Contents/Resources/cli-plugins/docker-compose /tmp/docker-nocreds/cli-plugins/docker-compose
echo '{"credsStore":""}' > /tmp/docker-nocreds/config.json
export DOCKER_CONFIG=/tmp/docker-nocreds
docker compose pull   # or build/push
```
This works because the images are public — no real credentials are ever needed, just a config that doesn't try the Keychain-backed helper at all. **Note:** even with this workaround, pulls occasionally still report a confusing empty-output error on the first attempt but succeed anyway (or need one retry) — verify success with `docker images` (check timestamps) rather than trusting the printed exit status alone. If you're scripting this yourself (rather than using the `deploy-*` targets) from a **Windows** machine invoking `ssh` via Node's `child_process`, use `execFileSync('ssh', [host, remoteCommand])` rather than `execSync('ssh ' + host + ' "' + remoteCommand + '"')` — Windows' `cmd.exe` quoting rules will otherwise mangle the embedded JSON's double quotes (this exact bug hit `pc2-deploy-lib.mjs` once — see its inline comment).

### If a fresh pull/build behaves inexplicably: suspect a corrupted local layer cache

If an image is verified correct on Docker Hub (matching digest, valid file contents when tested by pulling fresh on a different machine) but a specific machine still behaves as if running the broken old version, its local Docker layer cache may have a corrupted layer (this happened on PC2 after a disk-full incident corrupted a shared base-image layer, which then kept getting silently reused across every subsequent build with a different top-level tag). Fix: `docker compose down`, `docker system prune -a --force`, then `docker compose pull` again for a completely fresh download of every layer.

---

## Architecture

- PC1 (Windows) builds every image and pushes it to Docker Hub.
- PC2 (Mac) pulls the images and runs them with `docker compose`.
- **PC2 is Apple Silicon (arm64)**, but PC1 (Windows) builds on amd64 hardware. Every custom image is built **multi-platform** (`linux/amd64` + `linux/arm64` — see `platforms:` under each service's `build:` section in `docker-compose.yml`), so Docker Hub ends up with one manifest covering both architectures and each machine pulls the variant that matches it automatically. Multi-platform images can only be produced by pushing directly during the build (`docker compose build --push`) — building locally then pushing separately doesn't work, since the local Docker engine can only hold one architecture's image at a time.
- Postgres and Redis also run as containers on PC2 — one Postgres instance hosting all 9 service databases (created by an init script), not one instance per service.
- Jaeger, Prometheus, and Grafana also run as containers on the same `docker-compose.yml` — these are official upstream images (`jaegertracing/jaeger`, `prom/prometheus`, `grafana/grafana`), not built by us, just orchestrated alongside everything else. Bound to `127.0.0.1` only (ops dashboards, not meant to be internet-facing).
- All custom-built images are public on Docker Hub (Docker Hub's free plan only allows 1 private repo; since the GitHub source is already public, public images add no new exposure as long as no real secrets are baked into them).

## Resource limits (8GB host sizing)

Every service in `docker-compose.yml` sets `deploy.resources.limits.cpus`/`memory`, sized for an 8GB PC2 (e.g. a Mac mini M2, 8GB — the reference host this was sized for). `docker compose` (the v2 CLI) enforces `deploy.resources.limits` even outside Swarm mode, so this works exactly the same on `docker compose up -d` as it would in a Swarm stack — no extra flag needed.

| Service | CPU limit | Memory limit | Notes |
|---|---|---|---|
| postgres | 1.5 | 1024M | Single instance hosting all 9 service databases — the platform's heaviest single consumer |
| redis | 0.5 | 256M | |
| identity, tenant | 0.5 each | 256M each | |
| filemanager | 0.5 | 300M | `ffmpeg`-based image, slightly heavier than other .NET services |
| notification, translation, category, nasheed, backup | 0.3 each | 200M each | |
| ai | 1.0 | 1024M | The biggest wildcard — this limit assumes it calls out to an external AI API, not loading a local model. If AI.API ever loads a real local model, this limit needs to go up significantly and something else on this host has to come down to compensate |
| gateway | 0.5 | 256M | |
| admin, nasheed-admin, nasheed-web | 0.2 each | 64M each | Static nginx — negligible |
| jaeger, prometheus | 0.3 each | 300M each | Opt-in, see below |
| grafana | 0.3 | 256M | Opt-in, see below |

**Core stack total (everything except observability): ~4.3GB memory ceiling.** On an 8GB Mac, cap Docker Desktop's VM allocation at roughly 6GB (Docker Desktop → Settings → Resources), leaving ~2GB for macOS itself — that leaves ~1.7GB of headroom inside the VM for bursts above these per-container ceilings and for filesystem/network buffers Docker itself needs.

**Jaeger, Prometheus, and Grafana are gated behind the `observability` Compose profile — they do NOT start with a plain `docker compose up -d`.** They add ~850MB more if all three run, which doesn't reliably fit in the 8GB budget alongside the core stack under real load. Start them only when actively debugging:

```bash
docker compose --profile observability up -d     # core stack + dashboards
docker compose --profile observability down       # stop just the dashboards, or `down` with no profile to stop everything running
docker compose up -d                              # core stack only (default, no profile)
```

**If a container gets OOM-killed under load** (`docker inspect <service> --format '{{.State.OOMKilled}}'` reports `true`, or `docker compose ps` shows a climbing restart count with otherwise-normal logs): that container's limit is genuinely too tight for the load it's seeing, not a bug to silently raise without checking the tradeoff — raising one service's `memory:` limit in `docker-compose.yml` means either another service's limit has to come down, or the Docker Desktop VM allocation has to grow (i.e. the underlying host needs more RAM). Postgres and AI are the most likely candidates to need more under real usage — Postgres if tenant DB count grows, AI if it starts doing anything model-related locally. These limits are a ceiling to make failures predictable (one container dies loudly instead of the whole VM swapping and every container slowing down together) — they are a starting point sized for an 8GB host, not a permanent, load-tested budget.

## Files involved

| File                                                                                              | Purpose                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| ------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/Services/{Name}/{Name}.API/Dockerfile` (one per .NET service + Gateway)                      | Multi-stage build. **Build context must be this repo's root**, not the service folder — `docker build -f src/Services/Identity/Identity.API/Dockerfile .`                                                                                                                                                                                                                                                                                                                                |
| `src/Apps/Nasheed/Nasheed.API/Dockerfile`                                                         | Same pattern for the Nasheed app                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `src/Services/AI/AI.API/Dockerfile`                                                               | Python/FastAPI variant — also needs repo-root context, since `requirements.txt` has an editable install (`-e ../../../Shared/ihsandev_shared`) that resolves relative to `src/Shared/ihsandev_shared`                                                                                                                                                                                                                                                                                    |
| `src/Services/{Name}/{Name}.API/appsettings.Docker.json` (one per service + Gateway)              | Real per-service config for the Docker deployment. **Gitignored** (`*.Docker.json`) and **excluded from the Docker build context** (`.dockerignore`) — it only ever exists on disk on PC1/PC2, bind-mounted into the container at runtime via `docker-compose.yml`, never baked into an image. ASP.NET Core (and the Python AI service, via `ihsandev_shared.config.load_json_settings`) both read `ASPNETCORE_ENVIRONMENT` to pick this file up automatically — no code changes needed. |
| `.dockerignore` (repo root)                                                                       | Keeps `appsettings.Development.json` / `appsettings.Docker.json` / `bin`/`obj`/`venv` out of every build context                                                                                                                                                                                                                                                                                                                                                                         |
| `docker/postgres-init/init-databases.sh`                                                          | Creates all 8 service databases in the single Postgres container on first startup (only runs once, against an empty data volume)                                                                                                                                                                                                                                                                                                                                                         |
| `docker/logs/{service}/` (gitignored, PC2-local)                                                  | Bind-mounted into each backend service's container at its `Logging:FilePath` location (`/app/Logs`, or `/src/src/Services/AI/AI.API/Logs` for AI, since its `WORKDIR` differs) — makes logs persist across container recreation and directly readable from PC2's filesystem without `docker exec`. All 9 services with file logging get one (Gateway is console-only, no `FilePath`, so it's skipped).                                                                                |
| `docker/prometheus.yml`                                                                           | Docker-network variant of the root `prometheus.yml` — targets containers by service name (`identity:5001`, etc.) instead of `host.docker.internal`, which only works for the separate host-level dev setup                                                                                                                                                                                                                                                                               |
| `docker-compose.yml` (repo root)                                                                  | Orchestrates everything: Postgres, Redis, Jaeger, Prometheus, Grafana, all 10 backend containers, all 3 frontend containers. Assumes `MicroservicesArchitecture-Web` is cloned as a sibling folder (`../MicroservicesArchitecture-Web`) — same layout on PC1 and PC2.                                                                                                                                                                                                                     |
| `.env` (gitignored, copy from `.env.example`)                                                     | Holds `DOCKERHUB_USERNAME` (used by `docker-compose.yml`'s `image:` fields), `CORS_EXTRA_ORIGINS` (see the CORS pitfall below), and `PC2_SSH_HOST`/`PC2_REPO_PATH` (used by the `deploy-*` scripts)                                                                                                                                                                                                                                                                                     |
| `docker/build-and-push.mjs`                                                                       | One-click build + push script for **everything** — wraps `docker compose build --push` so there's a single source of truth (the compose file). `--push` is required (not optional) because every service builds multi-platform.                                                                                                                                                                                                                                                         |
| `docker/detect-changed-services.mjs`                                                              | Shared change-detection logic (git diff — uncommitted changes first, falls back to the last commit) — maps changed file paths to compose service names. Imported by `build-changed.mjs`, `deploy-changed.mjs`, and `build-and-deploy-changed.mjs` so all three agree on what "changed" means.                                                                                                                                                                                          |
| `docker/build-changed.mjs`                                                                        | Rebuilds+pushes only the services `detect-changed-services.mjs` reports as changed. Use this for routine builds instead of rebuilding all 12 every time. Build-only — does not deploy to PC2.                                                                                                                                                                                                                                                                                            |
| `docker/pc2-deploy-lib.mjs`                                                                       | Shared PC2-deploy logic — reads `PC2_SSH_HOST`/`PC2_REPO_PATH` from `.env`, SSHes in, and runs `git pull` + `docker compose pull` + `docker compose up -d` for the given services (or every service if none given). The `git pull` step (added August 2026) is what actually picks up any tracked-file change — like `docker-compose.yml`'s own `deploy.resources.limits`/`profiles` — that isn't baked into an image; without it, PC2's on-disk `docker-compose.yml` only ever changes via a manual `git pull` you remember to run yourself. Handles the macOS non-interactive-SSH Docker Hub credential workaround automatically (see below) so you don't need to run it by hand.                                                                                                                                            |
| `docker/deploy-pc2.mjs`                                                                           | Deploys given services (or everything) to PC2 — thin wrapper around `pc2-deploy-lib.mjs`. Bound to the `deploy-all` Nx target with no args.                                                                                                                                                                                                                                                                                                                                                |
| `docker/deploy-changed.mjs`                                                                       | Re-runs `detect-changed-services.mjs` and deploys that set to PC2 — use after `build-changed` has finished pushing.                                                                                                                                                                                                                                                                                                                                                                       |
| `docker/build-and-deploy-changed.mjs`                                                             | The one-command routine-deploy script: detects changed services **once**, builds+pushes them, then deploys that exact same list to PC2 — avoids any drift between a separate build-changed + deploy-changed if files change on disk in between.                                                                                                                                                                                                                                        |
| `MicroservicesArchitecture-Web/tools/docker/project.json`                                         | Dedicated Nx project (`docker`) grouping every Docker-related target separately from `admin`'s own targets — `build-identity`, `build-tenant`, ... one per backend service and per frontend app, plus `build-all`, `build-push-all`, `build-changed`, `deploy-all`, `deploy-changed`, and `build-deploy-changed`. **Every `build-*` target builds AND pushes** (multi-platform builds can't be loaded locally without pushing) — there is no "build only, no push" option per service.  |
| `MicroservicesArchitecture-Web/apps/{admin,nasheed/admin,nasheed/web}/Dockerfile` + `nginx.conf`  | Frontend: multi-stage Node build → static files served by nginx                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `MicroservicesArchitecture-Web/apps/{admin,nasheed/admin}/src/environments/environment.docker.ts` | Docker-target API URLs — currently `ihsandev.gleeze.com` (a free DDNS hostname pointed at PC2, so it survives PC2's public IP changing). **Baked in at build time** — changing the hostname requires editing this file _and_ rebuilding, not just re-running `docker compose up`                                                                                                                                                                                                         |

## Known limitations of the current (DDNS hostname, no TLS) setup

- **Addressing uses a free DDNS hostname** (`ihsandev.gleeze.com`) pointed at PC2 — stable across PC2's public IP changing (unlike a raw IP), but it's not a registered domain with DNS control, so it can't get a Let's Encrypt cert through normal domain-ownership validation the same way a real domain could.
- **Every backend service's port used to be published on every interface, not just the Gateway's** — the frontend's `environment.docker.ts` calls each service directly on its own port (mirroring how `environment.ts` works in local dev), so `docker-compose.yml` publishes ports 5000–5010, not only 5000. **Fixed in the July 2026 security audit**: the 9 backend service ports (5001, 5002, 5004–5010) are now bound to `127.0.0.1:{port}:{port}` — reachable from PC2 itself (Postman, `curl localhost:500x`, `docker logs`) but not from any other machine, including via router port-forwarding. Only Gateway (5000), admin (4200), nasheed-admin (4300), and nasheed-web (4301) remain reachable from outside PC2. **This means the frontend's current pattern of calling each backend service directly (not just through the Gateway) will fail for any real end user outside PC2** unless/until the frontend HTTP-client refactor mentioned above happens — routing every call through the Gateway is the only way to restore full external functionality without reopening the 9 backend ports.
  - **Frontend host ports changed from 80/8081/8082 to 4200/4300/4301 in August 2026** to match the local-dev ports (`ng serve` defaults/`project.json` overrides for `admin`/`nasheed-admin`/`nasheed-web`), so the same port number identifies the same app whether it's reached via `dotnet`/`ng serve` locally or via this Docker deployment. This means `admin`'s public URL now requires the port explicitly (`http://ihsandev.gleeze.com:4200`, not the previously-implicit `:80`) — update bookmarks, router port-forwarding rules, and `CORS_EXTRA_ORIGINS` in `.env` accordingly (see the CORS pitfall below).
  - **FileManager's case is already fixed, without needing that frontend refactor.** Unlike every other service, FileManager doesn't return file URLs the frontend builds itself from `environment.docker.ts` — every returned URL is built server-side from the single `FileManagerOptions:RootStoragePath` config value (`FileManagerService.cs`'s `_urlPrefix`), and since it's read fresh on every API response (never baked into the database — `FileManagerEntity.Path` stores a bare relative path like `ihsandev/system/image/{uuid}.webp`), repointing that one config value retroactively fixes every existing file link with no data migration. Fixed (August 2026) by setting `RootStoragePath` to `http://ihsandev.gleeze.com:5000` (Gateway) instead of `:5005` (FileManager's own blocked port), plus a new Gateway route (`filemanager-static-files-route`, `Gateway.API/appsettings.json`) forwarding anything not claimed by another route to FileManager — safe as a broad catch-all specifically because FileManager's static file server (`app.UseStaticFiles`, `RequestPath = ""` in `FileManager.API/Program.cs`) is the only thing in this platform serving bare-root paths with no `/api/v1/` prefix. `img-src`/`media-src` in `apps/admin` and `apps/nasheed/admin`'s `nginx.conf` needed the same origin added (`http://ihsandev.gleeze.com:5000`) alongside the direct-port entry, matching `connect-src`'s existing pattern (see `Angular.instructions.md` pitfall #9).
  - **Notification's SignalR hub connection needed the frontend refactor instead** (fixed August 2026, same day) — `libs/shared/src/lib/services/signalr.service.ts`'s `SignalrService.initializeConnection` builds the hub URL itself from `environment.apiUrls.notification`, so unlike FileManager there was no single server-side config value to repoint. Fixed by pointing it at `environment.apiUrls.gateway` instead, plus a new Gateway route (`notification-hub-route`, `Order: 10` — must be lower than `filemanager-static-files-route`'s `Order: 100`, since `/hubs/notifications/*` is outside the `/api/v1/` space just like FileManager's static files and would otherwise fall through to that catch-all). YARP proxies the WebSocket upgrade transparently — no special `Transforms` needed, unlike the `ai-stream-route`'s SSE handling. Since this fix lives in `libs/shared`, it covers both `apps/admin` and `apps/nasheed/admin` from one change. **Don't forget `connect-src`'s `ws://` entry is a separate scheme+origin from the `http://` one** — moving the hub connection from Notification's direct port to Gateway means the CSP's `ws://ihsandev.gleeze.com:5004` entry has to become `ws://ihsandev.gleeze.com:5000`, not just adding the new origin alongside the old one; the browser reported this as its own separate CSP violation (`Connecting to 'ws://...:5000/...' violates ... connect-src ... ws://ihsandev.gleeze.com:5004`) even after the negotiate handshake itself already succeeded through the new route. **If another service ever needs this same treatment, check first whether it already builds its own returned URLs server-side (cheap, retroactive fix, like FileManager) or whether the frontend constructs the URL itself from `environment.docker.ts` (needs this bigger frontend-plus-Gateway-route fix instead, like Notification's SignalR connection did).**
  - **Nasheed's own domain services (`libs/nasheed/shared/src/lib/nasheed-shared/services/{song,artist,search,ingestion-job,generation}.service.ts`) had this exact bug too, undetected until August 2026** — every one of them built its base URL from `environment.apiUrls['nasheed']` (the direct, now-`127.0.0.1`-only port `5009`) instead of `environment.apiUrls.gateway`, unlike every other domain service in `libs/core` (`TenantService`, `CategoryService`, `TranslationService`, etc.), which all already routed through the gateway from the start. This is why `nasheed-web`/`nasheed-admin`'s songs/artists/search/ingestion/generation pages worked from PC2 itself (or over a LAN where the direct port happens to still be reachable) but failed with `ERR_CONNECTION_REFUSED` for any real external user — the exact same failure class as Notification's SignalR case, just missed during that pass since it's a different (app-specific) lib. **Fixed** by pointing all five at `environment.apiUrls.gateway` instead — no Gateway route changes needed, since `nasheed-songs-route`/`nasheed-artists-route`/`nasheed-search-route`/`nasheed-ingestion-route`/`nasheed-generation-route` (`Gateway.API/appsettings.json`) already proxy those exact paths to the `nasheed` cluster, which was already correctly pointed at `http://nasheed:5009` (the Docker network hostname) in `appsettings.Docker.json`. **When auditing for this class of bug, don't stop at `libs/core` and `libs/shared` — check every app-specific shared lib (`libs/{app}/shared`) for the same direct-port anti-pattern.**
- **Secrets are reused from local dev** (`appsettings.Docker.json` was created as a copy of `appsettings.Development.json` with hostnames adjusted for Docker networking) — fine for personal/testing use, but rotate the Postgres password, JWT secret, and service shared secret before any real public exposure. Since these files aren't templated, a rotation means editing the value in **every** `appsettings.Docker.json` individually, and updating `POSTGRES_PASSWORD` in `.env` to match (`docker-compose.yml` reads it via `${POSTGRES_PASSWORD}`, not a hardcoded literal, as of the July 2026 audit).
- **No TLS yet** — plain HTTP over the DDNS hostname. See the chat history / root `CLAUDE.md` for the Cloudflare Tunnel alternative if HTTPS is needed sooner (it works fine on top of a DDNS-backed host).
- The existing `docker-compose.redis.yml` / `docker-compose.postgres-replication.yml` / `docker-compose.observability.yml` are separate, host-level dev-time compose files — do not run them alongside `docker-compose.yml` (container name collisions: both define `jaeger`/`prometheus`/`grafana`/`redis`-equivalent containers). `docker-compose.yml` is the fully-containerized equivalent for PC2; the three host-level files stay useful on PC1 for running the observability/DB stack next to services started via `dotnet run`.
- Grafana's default credentials in `.env` (`admin`/`admin`) are fine since Grafana is bound to `127.0.0.1` only, but change them before ever publishing that port beyond loopback.
- **Both Redis containers now require auth** — `docker-compose.redis.yml` (host-dev-mode Redis) was fixed in the July 2026 security audit, which found it published on every interface with zero auth; it's bound to `127.0.0.1:6379:6379` and runs with `--requirepass ${REDIS_PASSWORD}` (set in `.env`/`.env.example`). `docker-compose.yml`'s own `redis` container was left unauthenticated at the time (it was already `127.0.0.1`-only, so the audit treated it as lower priority) but was brought in line afterward for consistency — it now runs the same `--requirepass ${REDIS_PASSWORD}` command, with its healthcheck updated to `redis-cli -a ${REDIS_PASSWORD} ping` (a healthcheck without `-a` fails immediately once auth is required, which would otherwise permanently mark the container unhealthy and block every service's `depends_on: condition: service_healthy`). Every service's Redis connection string — both `appsettings.Development.json`/`appsettings.Test.json` (gitignored, real value; `dotnet run` mode, connects to `docker-compose.redis.yml`'s instance) and `appsettings.Docker.json` (connects to `docker-compose.yml`'s instance via the `redis` hostname) — includes `password=...` for this same reason. The tracked base `appsettings.json` carries a `CHANGE_ME_REDIS_PASSWORD` placeholder in both cases — same pattern as pitfall #16 in `.claude/instructions/Dotnet.instructions.md`.
- **`docker-compose.postgres-replication.yml`'s replication user password and `pg_hba.conf` rule were also hardened in the July 2026 audit**: `infrastructure/postgres/primary/01-setup-replication.sh` previously created the `replicator` user with a hardcoded password that ignored `$POSTGRES_REPLICATION_PASSWORD` entirely, and allowed replication connections from `0.0.0.0/0`/`::/0` (any source IP). Fixed to read the real env var and scope the `pg_hba.conf` rule to the `postgres-replica` service name (Docker's embedded DNS resolves it on the `postgres-replication` bridge network — the same hostname-based pattern the file's pre-existing "Allow connections from replica" rule already used). Both `5432`/`5433` are now bound to `127.0.0.1` too. `infrastructure/postgres/replica/postgresql.conf`'s checked-in copy is a reference only (the real one is regenerated by `pg_basebackup -R` at container startup) — its hardcoded password was replaced with a `CHANGE_ME_REPLICATION_PASSWORD` placeholder so it doesn't read as a live credential.

## Pitfall: every .NET service's tracked `appsettings.json` hardcodes `"Urls": "http://localhost:{port}"`

All 8 .NET services (Identity, Tenant, Notification, FileManager, Translation, Category, Nasheed, Gateway) have `"Urls": "http://localhost:{port}"` baked into their tracked, image-shipped `appsettings.json` (meant for convenient single-machine local dev). This value **wins over** the `ENV ASPNETCORE_URLS=http://+:{port}` set in each Dockerfile — the exact precedence mechanism isn't fully pinned down, but the observed effect is unambiguous: Kestrel logs `Now listening on: http://localhost:{port}`, meaning it's bound only to the container's own loopback interface, not all interfaces. Docker's port-forwarding still *accepts* the incoming TCP connection from outside the container (so `curl` connects successfully), but nothing is listening on the interface the connection actually lands on, so it gets reset immediately (`Recv failure: Connection reset by peer`) — this happened identically on every one of the 8 services, all showing `Up` and normal CPU in `docker compose ps`/`docker stats`, giving no indication anything was wrong short of actually trying to hit an endpoint.

**Fix:** every service's `appsettings.Docker.json` explicitly sets `"Urls": "http://+:{port}"` — since it's loaded after the base `appsettings.json` in the same configuration provider chain, it reliably overrides the same key through the exact mechanism the app already uses (rather than relying on environment-variable precedence, which doesn't win here for reasons not fully understood). **This requires no image rebuild** — `appsettings.Docker.json` is bind-mounted at runtime, not baked into the image, so a `git pull` on PC2 plus a container restart is enough. AI is unaffected (its Python entrypoint hardcodes `--host 0.0.0.0` directly in the `uvicorn` command, not read from `appsettings.json`). When adding a new .NET service to this pattern, set `"Urls": "http://+:{port}"` in its `appsettings.Docker.json` from the start — don't assume the `ENV ASPNETCORE_URLS` in the Dockerfile is sufficient.

## Pitfall: every .NET service needs `libicu-dev` in its final stage on arm64

`mcr.microsoft.com/dotnet/aspnet:10.0`'s **arm64** variant is missing ICU (globalization) libraries. Without them, .NET's `CultureInfo` static initialization `FailFast`s at startup with `Couldn't find a valid ICU package installed on the system. Please install libicu (or icu-libs)...` — but the container doesn't just exit and get restarted (which would be obvious from `docker compose ps`): it kept showing `Up`, pinned at ~100% CPU, accepting TCP connections but resetting them immediately (`curl: Recv failure: Connection reset by peer`), which is a much more confusing failure mode than a clean crash-loop. **This hit all 8 .NET services identically** (Identity, Tenant, Notification, FileManager, Translation, Category, Nasheed, Gateway) the first time they ran on PC2 (Apple Silicon) — none of it showed up during PC1's amd64-only build/push cycle, since amd64's `aspnet:10.0` variant has ICU fine.

**Fix:** every .NET service's final stage installs `libicu-dev` via `apt-get`, right after the `FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final` line. Deliberately **not** using the alternative fix (`ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`, which sidesteps the crash by disabling globalization entirely) — that would risk breaking this project's Arabic/English culture-aware string comparisons and formatting, given how central i18n is here. When adding a new .NET service to this pattern, copy the `libicu-dev` install into its final stage too — this base image gap won't be obvious from a same-architecture (amd64-only) test.

## Pitfall: AI's `appsettings.Docker.json` bind mount path must NOT be `/app/...`

Every .NET service's `appsettings.Docker.json` is bind-mounted at `/app/appsettings.Docker.json`, since `/app` is their `WORKDIR`. **AI is different** — its Python config loader (`core/config.py`) resolves the file relative to its own script location, which lands at `/src/src/Services/AI/AI.API`, not `/app`. Copy-pasting the `.NET` mount path for AI means the file silently never gets found: no startup error, no crash — `load_json_settings()` just doesn't find a `Docker.json` to merge and falls back entirely to `appsettings.json`'s base defaults (`CHANGE_ME_DB_PASSWORD`, `CHANGE_ME_JWT_SECRET`, `CHANGE_ME_SHARED_SECRET`, `http://localhost:...` endpoints). The container can still start and appear healthy — the failure only surfaces once something tries to actually use one of those broken values (DB connection, a service-to-service call, trace export). Confirmed via a log line reading `OpenTelemetry tracing initialised → http://localhost:4317` instead of the configured `http://jaeger:4317`. **Fixed** — AI's mount target is `/src/src/Services/AI/AI.API/appsettings.Docker.json`, matching its actual `WORKDIR`, same as its log volume already correctly did. When adding a new Python service to this pattern, always verify its bind-mount target against its own `WORKDIR`/base directory — don't assume it matches the .NET services' `/app` convention.

## Pitfall: Backup is the one .NET service whose Dockerfile isn't a plain copy of the others

Every other .NET service only ever talks to Postgres through Npgsql — the `aspnet:10.0` runtime image is sufficient. **Backup shells out to the actual `pg_dump`/`pg_restore` binaries** (`Backup.Infrastructure/Services/PgToolRunner.cs`), so its Dockerfile's final stage additionally installs `postgresql-client` alongside the usual `libicu-dev`, and `appsettings.Docker.json` points `Backup:PgDumpPath`/`PgRestorePath` at `/usr/bin/pg_dump`/`/usr/bin/pg_restore` (confirmed present at that path after `apt-get install postgresql-client` on this base image — Debian's `update-alternatives` symlinks the versioned binary there when only one client version is installed). The installed client is whatever major version Ubuntu's noble repos default to (16, as of this writing) against a `postgres:15-alpine` server — fine for plain dump/restore, which is compatible across adjacent major versions; pin the PGDG apt repo instead if an exact version match is ever required. Backup also gets its own named volume (`ihsandev_backups:/app/Backups`) so local dump files survive container recreation, unlike every other service which either has no persistent local state or uses a bind-mounted `Logs` folder only. **When adding another service that needs a native Postgres client tool (not just Npgsql), copy Backup's Dockerfile pattern, not a generic service's.**

## Pitfall: `Cors.AllowedOrigins` in `appsettings.Docker.json` must match the frontend's ACTUAL hostname, not an old one

Identity, Tenant, Translation, and FileManager's `appsettings.Docker.json` were found (checked during a documentation pass) still pointing `Cors.AllowedOrigins` at `http://anashid.ddnsfree.com` — an earlier DDNS hostname — while the frontend (`environment.docker.ts` for both `admin` and `nasheed-admin`) has actually been building against `ihsandev.gleeze.com` for a while. This class of bug is dangerous precisely because it's invisible to the verification method used everywhere else in this guide: `curl`/server-to-server calls never send an `Origin` header, so they succeed identically whether CORS is configured correctly or not — only an actual browser-based request (real admin panel usage) trips the mismatch, coming back as an opaque CORS error in the browser console with no corresponding server-side symptom.

**Original fix (superseded, kept for history):** every service's `Cors.AllowedOrigins` had to list every real frontend origin that calls it, edited individually in each service's `appsettings.Docker.json`. This worked but didn't scale — Identity, Tenant, Category, FileManager, Notification, and AI each carry their own copy of the origins list, so every hostname change (or new origin like `http://localhost` for local testing) meant editing N files, `scp`-ing all of them to PC2, and restarting N containers. This exact gap kept resurfacing (August 2026: three separate rounds of "add this origin" across the same services) — a strong signal the per-service-file approach itself was the bug, not any single missing origin.

**Current fix (August 2026): a single environment variable, `CORS_EXTRA_ORIGINS`, merged into every service's normal CORS check.** `CorsOriginsHelper.ResolveOrigins` (`IhsanDev.Shared.Infrastructure/Extensions/CorsOriginsHelper.cs`) reads this comma-separated env var and unions it with whatever `Cors:AllowedOrigins` the service's `appsettings.Docker.json` already configures — called from `TenantAwareCorsMiddleware` (Identity, FileManager, Notification, Category, Nasheed, PolySnap) and from `Tenant.API/Program.cs`'s inline CORS setup. AI (Python/FastAPI, `main.py`) reads the same `CORS_EXTRA_ORIGINS` env var directly (`os.environ.get(...)`) and merges it with `settings.Cors.AllowedOrigins`, mirroring the .NET helper without depending on pydantic-settings' own env-binding behavior. `docker-compose.yml` passes `CORS_EXTRA_ORIGINS: ${CORS_EXTRA_ORIGINS:-}` to identity, tenant, notification, filemanager, category, ai, and nasheed (not translation or backup — both already use `AllowAnyOrigin()` unconditionally in code and were never origin-restricted; not gateway — it has no CORS middleware at all, YARP just proxies whatever the downstream service returns). The value itself lives in `.env`'s `CORS_EXTRA_ORIGINS` (gitignored, per-machine, exactly like every other secret in this file).

**When moving to a new server: update ONLY `.env`'s `CORS_EXTRA_ORIGINS`** (plus `environment.docker.ts`'s hostname and a rebuild of `admin`/`nasheed-admin`, per the existing hostname-change note above) — no more touching seven separate `appsettings.Docker.json` files. Include every real frontend origin for the new host: `:4200` (admin), `:4300` (nasheed-admin), `:4301` (nasheed-web), both `http` and `https` if unsure which is live, and `http://localhost` if you ever test a locally-run Docker frontend image against this backend (its baked-in `environment.docker.ts` always calls the real production Gateway regardless of where the container itself runs, so the browser's `Origin` header is `http://localhost` while the request target is the production hostname). After changing `.env`, `docker compose up -d` (not `restart` — env var changes require recreating the container, `restart` reuses the already-materialized environment) for every affected service.

**This requires a rebuild** for the .NET services (the merge logic is compiled code, not config) — unlike the old per-file fix, which only needed a `scp` + restart. This is a one-time cost to switch to the new mechanism; every *subsequent* hostname/origin change is `.env`-only and needs no rebuild, since the env var itself is read fresh at container start.

**`CorsOriginsHelper.ResolveOrigins` matches origins by exact string equality (`StringComparer.OrdinalIgnoreCase`), not by hostname/prefix — a bare `http://localhost` entry does NOT also allow `http://localhost:4200`.** After the August 2026 frontend port change (admin/nasheed-admin/nasheed-web moved from 80/8081/8082 to 4200/4300/4301 — see "Known limitations" above), testing `ng serve` locally (`http://localhost:4200`) against the remote PC2 gateway failed with a preflight CORS rejection even though `.env`'s `CORS_EXTRA_ORIGINS` already had a bare `http://localhost` entry — that entry was added back when the locally-run case was a Docker image on port 80, and doesn't cover a dev-server origin with an explicit port. **Fixed** by adding `http://localhost:4200`, `http://localhost:4300`, and `http://localhost:4301` to `CORS_EXTRA_ORIGINS` alongside the existing bare `http://localhost` entry. **Whenever a frontend app's local dev or locally-run-Docker port changes, add the exact new `http://localhost:{port}` origin to `CORS_EXTRA_ORIGINS` — don't assume a bare `http://localhost` entry covers every port.**

## Pitfall: building and pushing an image is not the same as deploying it — verify both independently

`docker compose build --push <service>` only updates Docker Hub; the container on PC2 keeps running whatever image it was created with until something explicitly pulls and recreates it. This bit Gateway specifically: it was rebuilt and pushed as part of a larger batch, but the deploy step afterward only targeted a different subset of services — Gateway's container silently kept running a 3-week-old image (confirmed via `docker inspect gateway --format '{{.Created}}'` vs. `docker images ... --format '{{.CreatedAt}}'` showing two different dates) with no error anywhere. **When verifying a deploy, check both the image push succeeded AND the target container's creation timestamp is recent** (`docker inspect <service> --format 'Container Created: {{.Created}}'`) — a successful `build --push` log tells you nothing about whether PC2 ever actually picked it up. Using `nx run docker:build-deploy-changed` (or `build-changed` immediately followed by `deploy-changed`, from the *same* detected list) avoids this class of gap entirely, since the deploy step always targets exactly what was just built.

## Pitfall: every deploy orphans the previous image, and PC2's disk has almost no margin for that to accumulate

`docker compose pull` replaces whatever image a service's `:latest` tag pointed at, but the old image itself doesn't disappear — it just loses its tag and becomes a dangling `<none>` image, invisible in `docker compose ps` and easy to forget about. Found August 2026 after PC2's disk hit 100% full and crashed Postgres (see the "Recovering from a disk-full Docker crash" section below): `docker system df` showed **87 images totaling 20.52GB, with 13.29GB (64%) dangling** — roughly a week's worth of orphaned builds nobody had pruned, on a host (Mac mini M2, 8GB) with barely any spare disk margin to begin with (Xcode's `CoreSimulator`/`DerivedData`/`DeviceSupport` alone routinely eat 50-75GB of the same volume). `docker image prune -a -f` reclaimed the full 13.86GB with zero effect on the running stack — it's a hard Docker guarantee that an image still referenced by any container (running or stopped) is never removed, so every currently-deployed service's image was untouched.

**Fixed permanently**: `deployToPc2` (`docker/pc2-deploy-lib.mjs`) now runs `docker image prune -a -f` as the last step of every deploy, after `docker compose up -d` — so this no longer requires a manual/periodic prune. If you ever deploy to PC2 by some other path (a raw `ssh` + manual `docker compose pull`/`up -d`, bypassing the `nx run docker:deploy-*` targets), run `docker image prune -a -f` yourself afterward.

## Recovering from a disk-full Docker crash

Symptoms are misleading: `docker ps` can report a container as `Up ... (healthy)` while `docker exec`/`docker logs` on that same container simultaneously fail with `input/output error` or `"container is not running"` — the daemon's own storage layer is corrupted by disk-full I/O errors, not the container itself. In the worst case (host disk truly at 0 free) the daemon doesn't even error, it just hangs forever on any command.

1. Check real free space first: `df -h /System/Volumes/Data` on PC2 (or via `ssh <PC2_SSH_HOST>`). Below ~1GB free, expect Docker Desktop's VM disk (`~/Library/Containers/com.docker.docker/Data/vms/0/data/Docker.raw`) to start failing writes.
2. Find what's actually consuming space — don't assume it's Docker: `du -sh ~/Library/Developer/Xcode/DerivedData`, `~/Library/Caches`, and `docker system df` (images/build cache) are the usual suspects, in roughly that order of how fast they refill.
3. Free enough real disk space first (see above) — if the daemon is only *degraded* (commands return I/O errors but eventually respond), this alone can be enough for it to self-recover once headroom exists.
4. If the daemon is fully hung (commands never return, not even with an error), freeing disk space alone won't unwedge it — restart Docker Desktop itself: `killall Docker` (or `osascript -e 'quit app "Docker"'`) then `open -a Docker` on PC2, wait ~30-60s for the VM to reinitialize, then `docker ps -a` to confirm. All `restart: unless-stopped` containers come back automatically; Postgres will run a normal WAL-recovery replay on its next start (`database system was not properly shut down; automatic recovery in progress` in its logs) — this is expected and not itself data loss.
5. Once responsive again, run `docker image prune -a -f` (see the pitfall above) to reclaim any dangling images, and check `Docker.raw`'s on-disk size (`du -sh` on the path above) before and after — macOS does reclaim the freed blocks from this sparse file live, no restart needed, but it can lag the `docker system df` numbers by a few seconds.

## Pitfall: `docker compose up -d` does not restart a container just because a bind-mounted file's *content* changed

`appsettings.Docker.json` is bind-mounted, not baked into the image — editing it and then running `docker compose up -d <service>` looks like it should pick up the change, but Compose only recreates a container when it detects a change to the **service definition** (image tag, env vars, volume *paths*, etc.), not when a file *underneath* an already-configured bind mount changes on disk. The output even says `Container <service> Running` (not `Recreated`/`Started`) when this happens — easy to miss. The already-running process never re-reads the file, so the fix silently doesn't take effect. **After editing any bind-mounted config file and copying it to PC2, use `docker restart <service>`** (or `docker compose restart <service>`) explicitly — don't rely on `up -d` to notice a content-only change. The `deploy-*` Nx targets always call `docker compose up -d`, which is correct for *image* changes (a new image tag digest does trigger recreation) but not sufficient on its own right after a manual `scp` of a config file — restart explicitly in that specific case.

## Image size notes

- **`FileManager`'s image (~1GB vs ~450MB for other .NET services) is expected** — its Dockerfile installs `ffmpeg` via `apt-get`, which pulls in a large tree of codec/format libraries. Not a bug.
- **Any Python service with an ML dependency that transitively pulls in `torch` (e.g. `sentence-transformers`, as `AI.API` does) must pin the CPU-only wheel explicitly, or the image balloons by several GB.** The default PyPI `torch` wheel on Linux bundles full NVIDIA CUDA runtime libraries (`nvidia-cublas`, `nvidia-cudnn`, etc.) regardless of whether the deployment target has a GPU — this took `AI.API`'s image from 9GB down to 1.16GB once fixed. Fix pattern (see `src/Services/AI/AI.API/Dockerfile`):
  1. Multi-stage build — `build-essential`/compilers only exist in the discarded builder stage, never the final image.
  2. Before installing `requirements.txt`, run `pip install --index-url https://download.pytorch.org/whl/cpu torch` so the resolver already has a CPU wheel satisfied and never reaches for the CUDA one.
  3. `pip install --user` in the builder stage, then `COPY --from=build /root/.local /root/.local` into the final stage (plus the editable-install source paths it points at) — avoids carrying the builder stage's compiler toolchain into the final image.
  4. The final stage still needs `libgomp1` installed via `apt-get` — torch's CPU wheel dynamically links OpenMP and doesn't bundle it; omitting this causes an import error at runtime, not a build-time error.

## Multi-architecture builds (PC1 is amd64, PC2 is Apple Silicon)

Every custom service in `docker-compose.yml` sets `build.platforms: [linux/amd64, linux/arm64]`, so Docker Hub ends up with one manifest covering both architectures and each machine's `docker compose pull` grabs the one that matches it automatically.

**Critical pitfall: never let the .NET SDK build stage run under QEMU emulation.** The naive approach — just adding `platforms:` to the compose file with an unmodified `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build` — builds the SDK stage itself for `linux/arm64` via QEMU on the amd64 PC1 machine, and `dotnet restore`/`dotnet publish` reliably crash under that emulation with `qemu: uncaught target signal 6 (Aborted)` (a `NullReferenceException` deep in the JIT/thread pool internals) — this is a well-known .NET/QEMU incompatibility, not specific to this project. All 8 .NET Dockerfiles (7 services + Gateway) use the fix instead:

```dockerfile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN case "$TARGETARCH" in \
      amd64) RID=linux-x64 ;; \
      arm64) RID=linux-arm64 ;; \
      *) echo "Unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    dotnet restore "src/Services/{Name}/{Name}.API/{Name}.API.csproj" -r "$RID" && \
    dotnet publish "src/Services/{Name}/{Name}.API/{Name}.API.csproj" -c Release -o /app/publish -r "$RID" --self-contained false /p:UseAppHost=false
```

- `--platform=$BUILDPLATFORM` pins the build stage to the machine's own native architecture (amd64 on PC1) — **never emulated**, regardless of which `TARGETPLATFORM` is being built for.
- `dotnet publish -r <RID>` cross-compiles for the real target architecture without executing any target-architecture code during the build — .NET's SDK can produce `linux-arm64` output while running natively as `linux-x64`.
- Only the **final** stage (`mcr.microsoft.com/dotnet/aspnet:10.0`) needs to be the actual target architecture, and that's just a pre-built base image pull — no compilation happens there, so no emulation risk.
- The Python (`AI.API`) and Node (frontend) Dockerfiles did **not** need this treatment — pip/npm under QEMU emulation are simply slower, not crash-prone the way .NET's JIT is.

**When adding a new .NET service to this pattern, copy this exact structure** — a plain `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build` without `--platform=$BUILDPLATFORM` and the RID-based publish will silently work fine on PC1 alone (single-arch) and only fail once someone adds `platforms:` for multi-arch, which can be a confusing regression to trace back.

## Adding a new service to this pattern later

1. Create `appsettings.Docker.json` next to the service's `appsettings.Development.json` (copy Development values, swap `localhost` for the relevant Docker service name — `postgres`, `redis`, or another service's container name).
2. Create a `Dockerfile` in the service's API folder following the existing services' pattern (repo-root build context, `dotnet restore`/`publish` on that project only — ProjectReferences resolve automatically, no need to enumerate shared project paths). For a .NET service, copy the `--platform=$BUILDPLATFORM` + RID cross-compile structure from the "Multi-architecture builds" section above — don't just write a plain `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build`, or the arm64 build will crash under QEMU emulation the moment `platforms:` is added for it in `docker-compose.yml`.
3. Add the service to `docker-compose.yml`: `build`/`image`/`volumes` (bind-mount its `appsettings.Docker.json`)/`ports`/`depends_on`/`networks`. Include `build.platforms: [linux/amd64, linux/arm64]` — every other custom service does, so PC2 (Apple Silicon) can pull a native image instead of hitting "no matching manifest for linux/arm64/v8."
4. Add its database name to `docker/postgres-init/init-databases.sh`.
5. If it needs to be reachable through the Gateway, add its cluster (with the Docker service name as the address host) to `src/Gateway/Gateway.API/appsettings.Docker.json`.
6. Add a `deploy.resources.limits` block sized like the closest comparable existing service (a plain .NET service with no heavy dependency → `cpus: "0.3"`, `memory: 200M`; see the "Resource limits" section above for the full table) — an unbounded new container defeats the point of every other service already having a ceiling on an 8GB host.
