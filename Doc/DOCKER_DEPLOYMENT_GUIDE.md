# Docker Deployment Guide

**Description:** How to build, push, and run the full stack (9 backend services + 3 frontend apps) as Docker containers, using Docker Hub as the sync point between a Windows dev machine (PC1) and a Mac host machine (PC2).

---

## Quick Start: Deploying On a Fresh Server Pair

Follow this in order for a brand-new PC1/PC2 pair (or re-read it as a checklist if something's not working — most failures so far were silent, so re-verifying each step explicitly is worth it).

### 1. One-time setup on PC1 (the build machine)

1. Install Docker Desktop. Confirm multi-platform build support: `docker buildx ls` should list `linux/amd64` and `linux/arm64` under available platforms (Docker Desktop bundles QEMU for this automatically).
2. Clone **both** repos as sibling folders under the same parent directory (`docker-compose.yml` assumes this layout via `../MicroservicesArchitecture-Web`):
   ```
   SomeParentFolder/
     MicroservicesArchitecture/          (backend — this repo)
     MicroservicesArchitecture-Web/      (frontend)
   ```
3. In the backend repo root: copy `.env.example` → `.env`, fill in `DOCKERHUB_USERNAME` plus the Postgres/PgAdmin/Grafana credentials.
4. `docker login` with your Docker Hub account.
5. **If you want internet access, decide the hostname now, before building anything** — see step 6. `environment.docker.ts` (frontend) bakes the hostname in at build time; changing it later means a rebuild.

### 2. One-time setup on PC2 (the host machine)

1. Install Docker Desktop.
2. Clone **only the backend repo** — PC2 never builds anything, only pulls pre-built images, so the frontend repo isn't needed there at all.
3. **Transfer the gitignored files manually** (scp/AirDrop/USB — never `git pull`, since these are intentionally excluded from git and always will be):
   - `.env` (same one from PC1, or PC2-specific if credentials differ)
   - Every `appsettings.Docker.json` — one per backend service + Gateway (9 files total: Identity, Tenant, Notification, FileManager, Translation, Category, AI, Nasheed, Gateway)
   - Verify all 9 landed: `find . -name "appsettings.Docker.json" | wc -l` should print `9`. Missing one means that service silently runs on the base `appsettings.json`'s placeholder defaults with no startup error (see the AI bind-mount pitfall below for exactly this failure mode).
4. `docker login` (optional — raises the free-tier pull-rate limit above the ~100/hr anonymous ceiling, worth doing since this stack pulls ~17 images).
5. **Give PC2 a static/reserved LAN IP** (DHCP reservation on the router) before setting up any port forwarding — a rule pointing at a Wi-Fi-assigned IP will silently break if the lease changes.
6. **macOS-specific:**
   - Disable **AirPlay Receiver** (System Settings → General → AirDrop & Handoff) — it squats on port 5000, which Gateway needs. Without this, `docker compose up -d` fails with `address already in use` on port 5000.
   - **Do `docker login`/`docker compose pull` from an actual Terminal.app session, not SSH** — a non-interactive SSH session can't unlock the macOS Keychain, which Docker's credential helper needs even for anonymous/public pulls, producing `keychain cannot be accessed because the current session does not allow user interaction`. If you must do it over SSH anyway, see "SSH + Docker Hub credential workaround" below.

### 3. Build and push (from PC1)

```powershell
nx run docker:build-push-all      # first deploy — builds and pushes all 12 images
nx run docker:build-changed       # routine deploys — only rebuilds services that actually changed (git diff-based)
nx run docker:build-identity      # one specific image — see tools/docker/project.json for the full list
```

First-time builds take a while — the AI image alone can take 20+ minutes for its arm64 variant (PyTorch install under QEMU emulation). Routine rebuilds are much faster since Docker's layer cache skips anything unchanged.

### 4. Deploy (on PC2)

```bash
docker compose pull
docker compose up -d
```

### 5. Verify — `docker compose ps` showing `Up` is NOT proof it works

Two of the four real bugs found during this deployment (ICU crash-loop, hardcoded `localhost` binding) both showed a completely normal `Up` status and unremarkable CPU in `docker compose ps`/`docker stats`, while being **entirely non-functional**. Always follow up with an actual request:

```bash
docker stats --no-stream                                            # CPU pinned near 100% on a .NET service = something's wrong even if status says Up
curl -sS -o /dev/null -w 'HTTP %{http_code}\n' http://localhost:5000/health   # repeat per service port
docker logs <service> --tail 30                                     # check for anything after "Application started"
```

### 6. Expose to the internet (optional)

1. Get a hostname pointed at PC2's public IP — either a real domain, or a free DDNS service (this deployment uses one) if you don't have a domain yet.
2. **Before building**, set that hostname in `environment.docker.ts` for `admin` and `nasheed-admin` (`MicroservicesArchitecture-Web/apps/{admin,nasheed/admin}/src/environments/environment.docker.ts`) — it's baked into the compiled JS at build time.
3. Forward these ports on your router to PC2's static LAN IP: **80, 5000–5009, 8081, 8082** (see "Known limitations" below for why every backend port needs forwarding, not just the Gateway's).
4. Test from **outside** your local network (e.g. phone on cellular data, not the same Wi-Fi) — a LAN-only test won't catch a missing port-forward rule.

### SSH + Docker Hub credential workaround (macOS, if you can't use Terminal.app directly)

If you must run `docker compose pull`/`build`/`push` over SSH on a Mac and hit the Keychain error above, point Docker at a temporary, credential-free config instead of fighting the Keychain:

```bash
mkdir -p /tmp/docker-nocreds-bin && cat > /tmp/docker-nocreds-bin/docker-credential-none <<'EOF'
#!/bin/sh
echo 'credentials not found in native keychain' >&2
exit 1
EOF
chmod +x /tmp/docker-nocreds-bin/docker-credential-none
mkdir -p /tmp/docker-nocreds/cli-plugins
ln -sf /Applications/Docker.app/Contents/Resources/cli-plugins/docker-compose /tmp/docker-nocreds/cli-plugins/docker-compose
echo '{"credsStore":"none"}' > /tmp/docker-nocreds/config.json
export PATH=/tmp/docker-nocreds-bin:/usr/local/bin:$PATH
export DOCKER_CONFIG=/tmp/docker-nocreds
docker compose pull   # or build/push
```
This works because the images are public — no real credentials are ever needed, just something that answers "not found" instead of the real Keychain-backed helper failing outright. **Note:** even with this workaround, pulls occasionally still report a confusing empty-output error on the first attempt but succeed anyway (or need one retry) — verify success with `docker images` (check timestamps) rather than trusting the printed exit status alone.

### If a fresh pull/build behaves inexplicably: suspect a corrupted local layer cache

If an image is verified correct on Docker Hub (matching digest, valid file contents when tested by pulling fresh on a different machine) but a specific machine still behaves as if running the broken old version, its local Docker layer cache may have a corrupted layer (this happened on PC2 after a disk-full incident corrupted a shared base-image layer, which then kept getting silently reused across every subsequent build with a different top-level tag). Fix: `docker compose down`, `docker system prune -a --force`, then `docker compose pull` again for a completely fresh download of every layer.

---

## Architecture

- PC1 (Windows) builds every image and pushes it to Docker Hub.
- PC2 (Mac) pulls the images and runs them with `docker compose`.
- **PC2 is Apple Silicon (arm64)**, but PC1 (Windows) builds on amd64 hardware. Every custom image is built **multi-platform** (`linux/amd64` + `linux/arm64` — see `platforms:` under each service's `build:` section in `docker-compose.yml`), so Docker Hub ends up with one manifest covering both architectures and each machine pulls the variant that matches it automatically. Multi-platform images can only be produced by pushing directly during the build (`docker compose build --push`) — building locally then pushing separately doesn't work, since the local Docker engine can only hold one architecture's image at a time.
- Postgres and Redis also run as containers on PC2 — one Postgres instance hosting all 8 service databases (created by an init script), not one instance per service.
- Jaeger, Prometheus, and Grafana also run as containers on the same `docker-compose.yml` — these are official upstream images (`jaegertracing/jaeger`, `prom/prometheus`, `grafana/grafana`), not built by us, just orchestrated alongside everything else. Bound to `127.0.0.1` only (ops dashboards, not meant to be internet-facing).
- All custom-built images are public on Docker Hub (Docker Hub's free plan only allows 1 private repo; since the GitHub source is already public, public images add no new exposure as long as no real secrets are baked into them).

## Files involved

| File                                                                                              | Purpose                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| ------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/Services/{Name}/{Name}.API/Dockerfile` (one per .NET service + Gateway)                      | Multi-stage build. **Build context must be this repo's root**, not the service folder — `docker build -f src/Services/Identity/Identity.API/Dockerfile .`                                                                                                                                                                                                                                                                                                                                |
| `src/Apps/Nasheed/Nasheed.API/Dockerfile`                                                         | Same pattern for the Nasheed app                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `src/Services/AI/AI.API/Dockerfile`                                                               | Python/FastAPI variant — also needs repo-root context, since `requirements.txt` has an editable install (`-e ../../../Shared/ihsandev_shared`) that resolves relative to `src/Shared/ihsandev_shared`                                                                                                                                                                                                                                                                                    |
| `src/Services/{Name}/{Name}.API/appsettings.Docker.json` (one per service + Gateway)              | Real per-service config for the Docker deployment. **Gitignored** (`*.Docker.json`) and **excluded from the Docker build context** (`.dockerignore`) — it only ever exists on disk on PC1/PC2, bind-mounted into the container at runtime via `docker-compose.yml`, never baked into an image. ASP.NET Core (and the Python AI service, via `ihsandev_shared.config.load_json_settings`) both read `ASPNETCORE_ENVIRONMENT` to pick this file up automatically — no code changes needed. |
| `.dockerignore` (repo root)                                                                       | Keeps `appsettings.Development.json` / `appsettings.Docker.json` / `bin`/`obj`/`venv` out of every build context                                                                                                                                                                                                                                                                                                                                                                         |
| `docker/postgres-init/init-databases.sh`                                                          | Creates all 8 service databases in the single Postgres container on first startup (only runs once, against an empty data volume)                                                                                                                                                                                                                                                                                                                                                         |
| `docker/logs/{service}/` (gitignored, PC2-local)                                                  | Bind-mounted into each backend service's container at its `Logging:FilePath` location (`/app/Logs`, or `/src/src/Services/AI/AI.API/Logs` for AI, since its `WORKDIR` differs) — makes logs persist across container recreation and directly readable from PC2's filesystem without `docker exec`. All 8 services with file logging get one (Gateway is console-only, no `FilePath`, so it's skipped).                                                                                |
| `docker/prometheus.yml`                                                                           | Docker-network variant of the root `prometheus.yml` — targets containers by service name (`identity:5001`, etc.) instead of `host.docker.internal`, which only works for the separate host-level dev setup                                                                                                                                                                                                                                                                               |
| `docker-compose.yml` (repo root)                                                                  | Orchestrates everything: Postgres, Redis, Jaeger, Prometheus, Grafana, all 9 backend containers, all 3 frontend containers. Assumes `MicroservicesArchitecture-Web` is cloned as a sibling folder (`../MicroservicesArchitecture-Web`) — same layout on PC1 and PC2.                                                                                                                                                                                                                     |
| `.env` (gitignored, copy from `.env.example`)                                                     | Holds `DOCKERHUB_USERNAME`, used by `docker-compose.yml`'s `image:` fields                                                                                                                                                                                                                                                                                                                                                                                                               |
| `docker/build-and-push.mjs`                                                                       | One-click build + push script for **everything** — wraps `docker compose build --push` so there's a single source of truth (the compose file). `--push` is required (not optional) because every service builds multi-platform.                                                                                                                                                                                                                                                         |
| `docker/build-changed.mjs`                                                                        | Detects which services actually changed (git diff — uncommitted changes first, falls back to the last commit) in both repos, maps changed paths to compose service names, and only rebuilds+pushes those. Use this for routine deploys instead of rebuilding all 12 every time.                                                                                                                                                                                                         |
| `MicroservicesArchitecture-Web/tools/docker/project.json`                                         | Dedicated Nx project (`docker`) grouping every Docker-related target separately from `admin`'s own targets — `build-identity`, `build-tenant`, ... one per backend service and per frontend app, plus `build-all`, `build-push-all`, and `build-changed`. **Every one of these targets builds AND pushes** (multi-platform builds can't be loaded locally without pushing) — there is no "build only, no push" option per service anymore.                                              |
| `MicroservicesArchitecture-Web/apps/{admin,nasheed/admin,nasheed/web}/Dockerfile` + `nginx.conf`  | Frontend: multi-stage Node build → static files served by nginx                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `MicroservicesArchitecture-Web/apps/{admin,nasheed/admin}/src/environments/environment.docker.ts` | Docker-target API URLs — currently `ihsandev.gleeze.com` (a free DDNS hostname pointed at PC2, so it survives PC2's public IP changing). **Baked in at build time** — changing the hostname requires editing this file _and_ rebuilding, not just re-running `docker compose up`                                                                                                                                                                                                         |

## Deploy flow

```powershell
# PC1 (one-time): copy .env.example to .env, set DOCKERHUB_USERNAME, then `docker login`

# PC1 (typical deploy — only rebuilds+pushes services whose source actually changed):
nx run docker:build-changed

# PC1 (force a full rebuild+push of all 12 images — e.g. first deploy, or after a Dockerfile/compose change):
nx run docker:build-push-all

# PC1 (build + push one specific image by name — always pushes, see note above):
nx run docker:build-identity     # or build-tenant, build-admin, build-ai, etc. — see tools/docker/project.json

# PC2 (every deploy): from this repo's root —
docker compose pull
docker compose up -d
```

## Known limitations of the current (DDNS hostname, no TLS) setup

- **Addressing uses a free DDNS hostname** (`ihsandev.gleeze.com`) pointed at PC2 — stable across PC2's public IP changing (unlike a raw IP), but it's not a registered domain with DNS control, so it can't get a Let's Encrypt cert through normal domain-ownership validation the same way a real domain could.
- **Every backend service's port is published**, not just the Gateway's. The frontend's `environment.docker.ts` calls each service directly on its own port (mirroring how `environment.ts` works in local dev), so `docker-compose.yml` publishes ports 5000–5009, not only 5000. If you forward ports on your router, you're exposing all of them, not just the Gateway — routing every frontend call through the Gateway only would require a frontend HTTP-client refactor, not attempted here.
- **Secrets are reused from local dev** (`appsettings.Docker.json` was created as a copy of `appsettings.Development.json` with hostnames adjusted for Docker networking) — fine for personal/testing use, but rotate the Postgres password, JWT secret, and service shared secret before any real public exposure. Since these files aren't templated, a rotation means editing the value in **every** `appsettings.Docker.json` individually, and updating `POSTGRES_PASSWORD` in `docker-compose.yml` to match.
- **No TLS yet** — plain HTTP over the DDNS hostname. See the chat history / root `CLAUDE.md` for the Cloudflare Tunnel alternative if HTTPS is needed sooner (it works fine on top of a DDNS-backed host).
- The existing `docker-compose.redis.yml` / `docker-compose.postgres-replication.yml` / `docker-compose.observability.yml` are separate, host-level dev-time compose files — do not run them alongside `docker-compose.yml` (container name collisions: both define `jaeger`/`prometheus`/`grafana`/`redis`-equivalent containers). `docker-compose.yml` is the fully-containerized equivalent for PC2; the three host-level files stay useful on PC1 for running the observability/DB stack next to services started via `dotnet run`.
- Grafana's default credentials in `.env` (`admin`/`admin`) are fine since Grafana is bound to `127.0.0.1` only, but change them before ever publishing that port beyond loopback.

## Pitfall: every .NET service's tracked `appsettings.json` hardcodes `"Urls": "http://localhost:{port}"`

All 8 .NET services (Identity, Tenant, Notification, FileManager, Translation, Category, Nasheed, Gateway) have `"Urls": "http://localhost:{port}"` baked into their tracked, image-shipped `appsettings.json` (meant for convenient single-machine local dev). This value **wins over** the `ENV ASPNETCORE_URLS=http://+:{port}` set in each Dockerfile — the exact precedence mechanism isn't fully pinned down, but the observed effect is unambiguous: Kestrel logs `Now listening on: http://localhost:{port}`, meaning it's bound only to the container's own loopback interface, not all interfaces. Docker's port-forwarding still *accepts* the incoming TCP connection from outside the container (so `curl` connects successfully), but nothing is listening on the interface the connection actually lands on, so it gets reset immediately (`Recv failure: Connection reset by peer`) — this happened identically on every one of the 8 services, all showing `Up` and normal CPU in `docker compose ps`/`docker stats`, giving no indication anything was wrong short of actually trying to hit an endpoint.

**Fix:** every service's `appsettings.Docker.json` explicitly sets `"Urls": "http://+:{port}"` — since it's loaded after the base `appsettings.json` in the same configuration provider chain, it reliably overrides the same key through the exact mechanism the app already uses (rather than relying on environment-variable precedence, which doesn't win here for reasons not fully understood). **This requires no image rebuild** — `appsettings.Docker.json` is bind-mounted at runtime, not baked into the image, so a `git pull` on PC2 plus a container restart is enough. AI is unaffected (its Python entrypoint hardcodes `--host 0.0.0.0` directly in the `uvicorn` command, not read from `appsettings.json`). When adding a new .NET service to this pattern, set `"Urls": "http://+:{port}"` in its `appsettings.Docker.json` from the start — don't assume the `ENV ASPNETCORE_URLS` in the Dockerfile is sufficient.

## Pitfall: every .NET service needs `libicu-dev` in its final stage on arm64

`mcr.microsoft.com/dotnet/aspnet:10.0`'s **arm64** variant is missing ICU (globalization) libraries. Without them, .NET's `CultureInfo` static initialization `FailFast`s at startup with `Couldn't find a valid ICU package installed on the system. Please install libicu (or icu-libs)...` — but the container doesn't just exit and get restarted (which would be obvious from `docker compose ps`): it kept showing `Up`, pinned at ~100% CPU, accepting TCP connections but resetting them immediately (`curl: Recv failure: Connection reset by peer`), which is a much more confusing failure mode than a clean crash-loop. **This hit all 8 .NET services identically** (Identity, Tenant, Notification, FileManager, Translation, Category, Nasheed, Gateway) the first time they ran on PC2 (Apple Silicon) — none of it showed up during PC1's amd64-only build/push cycle, since amd64's `aspnet:10.0` variant has ICU fine.

**Fix:** every .NET service's final stage installs `libicu-dev` via `apt-get`, right after the `FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final` line. Deliberately **not** using the alternative fix (`ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`, which sidesteps the crash by disabling globalization entirely) — that would risk breaking this project's Arabic/English culture-aware string comparisons and formatting, given how central i18n is here. When adding a new .NET service to this pattern, copy the `libicu-dev` install into its final stage too — this base image gap won't be obvious from a same-architecture (amd64-only) test.

## Pitfall: AI's `appsettings.Docker.json` bind mount path must NOT be `/app/...`

Every .NET service's `appsettings.Docker.json` is bind-mounted at `/app/appsettings.Docker.json`, since `/app` is their `WORKDIR`. **AI is different** — its Python config loader (`core/config.py`) resolves the file relative to its own script location, which lands at `/src/src/Services/AI/AI.API`, not `/app`. Copy-pasting the `.NET` mount path for AI means the file silently never gets found: no startup error, no crash — `load_json_settings()` just doesn't find a `Docker.json` to merge and falls back entirely to `appsettings.json`'s base defaults (`CHANGE_ME_DB_PASSWORD`, `CHANGE_ME_JWT_SECRET`, `CHANGE_ME_SHARED_SECRET`, `http://localhost:...` endpoints). The container can still start and appear healthy — the failure only surfaces once something tries to actually use one of those broken values (DB connection, a service-to-service call, trace export). Confirmed via a log line reading `OpenTelemetry tracing initialised → http://localhost:4317` instead of the configured `http://jaeger:4317`. **Fixed** — AI's mount target is `/src/src/Services/AI/AI.API/appsettings.Docker.json`, matching its actual `WORKDIR`, same as its log volume already correctly did. When adding a new Python service to this pattern, always verify its bind-mount target against its own `WORKDIR`/base directory — don't assume it matches the .NET services' `/app` convention.

## Pitfall: `Cors.AllowedOrigins` in `appsettings.Docker.json` must match the frontend's ACTUAL hostname, not an old one

Identity, Tenant, Translation, and FileManager's `appsettings.Docker.json` were found (checked during a documentation pass) still pointing `Cors.AllowedOrigins` at `http://anashid.ddnsfree.com` — an earlier DDNS hostname — while the frontend (`environment.docker.ts` for both `admin` and `nasheed-admin`) has actually been building against `ihsandev.gleeze.com` for a while. This class of bug is dangerous precisely because it's invisible to the verification method used everywhere else in this guide: `curl`/server-to-server calls never send an `Origin` header, so they succeed identically whether CORS is configured correctly or not — only an actual browser-based request (real admin panel usage) trips the mismatch, coming back as an opaque CORS error in the browser console with no corresponding server-side symptom.

**Fix:** every service's `Cors.AllowedOrigins` must list every real frontend origin that calls it — `http://ihsandev.gleeze.com` (admin, port 80, no port in the Origin header) and `http://ihsandev.gleeze.com:8081` (nasheed-admin). **This requires no rebuild** — same as the `Urls` fix, `appsettings.Docker.json` is bind-mounted, not baked in, so a `scp` + `docker compose restart <service>` on PC2 is enough. **When the DDNS hostname (or a real domain) ever changes, `Cors.AllowedOrigins` must be updated in every affected `appsettings.Docker.json` at the same time as `environment.docker.ts`** — they're two independent, easy-to-forget places carrying the same hostname, and only one of them (`environment.docker.ts`) is git-tracked, so the other is easy to silently miss during a hostname migration.

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
