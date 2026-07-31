# Security Audit — Pending Decisions

**Context:** A July 2026 full-codebase security audit found 54 findings across every backend service, the gateway, shared libraries, infrastructure config, and the three frontend apps. 48 of them were safe, isolated fixes and have been implemented, build-verified, and (for the backend/.NET side) test-verified — see the "Also reviewed, no issues found" boxes and updated sections in each service's own `Doc/*.md` file, and `.claude/instructions/Dotnet.instructions.md` pitfalls #19–#23, for what changed.

The items below were **deliberately not touched** — each needs a product/architecture decision, not just an engineering fix, or is an operational follow-up outside code changes. Read this file before starting any of them; each links to the original finding's reasoning.

---

## 1. FileManager access-control redesign

**What's still open:** Anonymous, unauthenticated file download by guessing an integer ID; the static-file server bypasses all auth/tenant checks; no ownership check on file reads (view/download/list) by ID.

**Why it's blocked on a decision:** Locking these down requires knowing which files are actually meant to be public. Nasheed song artwork, AI.API's multimodal file fetch, and possibly other embedded-image use cases may depend on files being reachable without a token today. Blindly requiring auth on every read breaks those call sites.

**Decision needed:** Add a visibility/scope field to the File entity (e.g. `Public` / `TenantOnly` / `Owner`) and decide the default. Once that model exists, gate downloads and the static-file server accordingly, and add ownership checks to by-ID reads for anything not explicitly public.

**Already done, safe on its own:** SVG Content-Disposition hardening (stops inline script execution), magic-byte upload validation, ffmpeg subprocess timeout, and uploader identity now derived from the JWT instead of a client-supplied field — none of these depend on the visibility decision above.

---

## 2. Identity token lifetime + session revocation

**What's still open:** Access tokens live 15 days (`AccessTokenExpirationMinutes: 21600`), logout only revokes the refresh token (fixed this pass), and there's no `jti` blacklist for immediate access-token revocation.

**Why it's blocked on a decision:** The Angular frontend's `_loadUserFromStorage` doesn't proactively refresh the access token — a page reload after the token naturally expires currently means a silent logout. Shortening the token lifetime now would make that latent frontend bug surface immediately and constantly, not just at the 15-day mark.

**Decision needed:** Either (a) fix the frontend's refresh-on-load/proactive-refresh flow first, then shorten the token lifetime and add the `jti` blacklist (requires a shared-library change so every service's JWT validation checks it, likely via Redis), or (b) accept the current lifetime as a deliberate tradeoff for now.

---

## 3. Frontend token storage — localStorage → httpOnly cookies

**What's still open:** Access and refresh tokens sit in `localStorage`, readable by any script on the page. The one safe, isolated piece (consolidating two direct `localStorage` calls in `token.interceptor.ts` into the existing `IdentityStorageService`) is already done.

**Why it's blocked on a decision:** Moving to httpOnly cookies means the Gateway or Identity service issues the cookie on login/refresh, needs CSRF protection added, and the frontend's auth flow (interceptors, SignalR token-passing, every API call's credential handling) all change together. This is a coordinated backend+frontend architecture change, not a quick fix.

**Decision needed:** Confirm the team wants to take this on, and sequence it (likely: design the cookie/CSRF contract → implement gateway-side issuance → migrate frontend → remove localStorage token paths).

---

## 4. FileManager storage quota

**What's still open:** Only a 100MB per-file cap exists; nothing tracks cumulative storage per user or tenant.

**Why it's blocked on a decision:** The actual limit is a product/business decision (how many GB per user? per tenant? does it vary by plan?), not something to invent. Also worth checking current storage usage before picking a number, so existing accounts aren't retroactively broken.

**Decision needed:** Pick a quota policy (flat limit, per-tenant configurable, or plan-based), then implement enforcement in `SaveFileAsync`.

---

## 5. Gateway-level authentication

**What's still open:** The gateway (`src/Gateway/Gateway.API`) still performs zero authentication of its own — it blindly proxies every route, relying entirely on each backend service's own auth. The 5 other Gateway fixes this pass (rate-limited `/health/aggregate`, scoped admin catch-all, HTTPS/HSTS, request timeout, server-generated correlation IDs) were kept deliberately independent of this.

**Why it's blocked on a decision:** Adding global JWT auth at the gateway means enumerating every route that must stay anonymous — login, register, OTP request/verify, forgot-password, the public tenant-lookup endpoint, health checks, refresh-token — and YARP's route-table-based proxying (not per-endpoint attributes) makes that enumeration more error-prone than a typical ASP.NET Core app. Getting it wrong either breaks legitimate anonymous flows or reopens the exact gap this is meant to close.

**Decision needed:** Enumerate the anonymous-route allowlist explicitly (probably as a reviewed list in `appsettings.json`), then wire `AddAuthentication`/`AddJwtBearer` + a default-deny policy at the gateway, treating each backend service's own auth as defense-in-depth rather than the only control.

---

## 6. Rotate the Postgres password

**What's still open:** `docker-compose.yml` hardcoded `POSTGRES_PASSWORD: "2230222"` and this was live on the **public** GitHub repo (`hadi6jokhadar/MicroservicesArchitecture`) until this session. The value has been:
- Replaced with `${POSTGRES_PASSWORD}` sourced from `.env` (committed and pushed).
- Scrubbed from all 164 commits of git history via `git filter-repo --replace-text`, force-pushed, and independently re-verified against a fresh clone from GitHub.

**Why it's still open:** The history scrub stops the value from being *newly* discoverable, but anyone who already cloned/forked the repo before the rewrite (or has it cached anywhere) still has the old password. The only complete mitigation is rotating the actual credential — `2230222` should be treated as burned regardless of the history cleanup.

**Decision needed:** Confirm you want it rotated now (this touches `.env`, every service's `appsettings.Docker.json`, and requires restarting the Postgres container plus every connected service — was pending your go-ahead as of this write-up since services were actively running for testing).

---

## 7. Sync any other clone of this repo after the history rewrite

**What's still open:** `origin/main`'s commit hashes changed for the entire history (old HEAD `ed8fe72` → new HEAD `6a96a95`, and every ancestor before it). Per `Doc/DOCKER_DEPLOYMENT_GUIDE.md`'s PC1/PC2 deployment setup, PC2 (or any other machine/clone) pulling this repo normally will hit a diverged-history error, not a clean fast-forward.

**Decision needed / action needed on any other clone:** Don't `git pull` normally there. Instead:
```powershell
git fetch origin
git reset --hard origin/main
```
This discards any local commits on that machine that aren't already pushed — check for uncommitted/unpushed work on that machine first.

---

## Not blocked — already fixed, no decision needed

For completeness, everything else from the 54 findings (cross-cutting middleware/secrets, Identity auth bugs, Gateway hardening minus item 5 above, FileManager's non-visibility-dependent fixes, Tenant/Category, Notification/Translation/Backup, AI/Nasheed, all 6 infrastructure binding/auth issues, and the 3 safe frontend fixes) has been implemented and verified. See each service's own `Doc/*.md` for the specifics, and `.claude/instructions/Dotnet.instructions.md` for the new pitfalls this pass surfaced (#19–#23), including the two pre-existing, unrelated test-infra bugs found while verifying (Nasheed's test factory had a hardcoded placeholder Postgres password that never worked; the shared `ValidateSecretStrength` check needed test-host detection to avoid breaking every service's integration tests).
