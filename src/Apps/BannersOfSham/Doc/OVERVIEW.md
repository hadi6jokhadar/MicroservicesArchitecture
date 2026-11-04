# Banners of Sham — Backend Game Service Overview

Backend game server for **Banners of Sham**, a mobile real-time strategy game. The game vision and Unity client are documented in `Unity/BannersOfSham/Doc/OVERVIEW.md` (monorepo root); the admin panel in `MicroservicesArchitecture-Web/apps/BannersOfSham/Doc/OVERVIEW.md`.

**Status:** not scaffolded yet — this folder holds only the docs. It will be created with the `new-service` agent; at that point add the other required `Doc/` files (`DOCUMENTATION_INDEX.md`, `ENTITIES_AND_DATA_MODEL.md`, `API_ENDPOINTS.md` — see `Doc/NEW_SERVICE_INTEGRATION_GUIDE.md`).

## Planned setup

| Item | Value |
|---|---|
| Location | `src/Apps/BannersOfSham/` |
| Projects | `BannersOfSham.API`, `.Application`, `.Domain`, `.Infrastructure`, `.Worker`, `.Contracts` |
| Tenant ID | `BannersOfSham` (fixed) — not registered yet; confirm the exact value when scaffolding |
| Port | 5012 (next free) — not reserved yet |
| Database strategy | Per-tenant DB (Strategy B, same as PolySnap) — with a fixed tenant this is one dedicated game database |

## Platform reuse

The game is another app with a fixed tenant ID, the same way Nasheed and PolySnap are:

| Platform piece | Game use |
|---|---|
| Identity (5001) | Player accounts, JWT for the Unity client |
| Notification (5004) | Push notifications and SignalR real-time updates |
| FileManager (5005) | Avatars, alliance banners, downloadable content |
| Translation (5006) | Localized game text (Arabic + RTL) |
| Backup (5010) | Game database backups |
| Gateway | Single entry point for the Unity client |

## Architecture decisions

- **Tenant ≠ world.** The tenant ID is fixed and only identifies the app. Game worlds/kingdoms are domain entities with a `WorldId`, never tenants. Sharding, if needed, is by `WorldId` inside this service.
- **One game service, modular monolith.** City, buildings, troops, marches, combat and the world map change together transactionally, so they live in this one service. Only independent contexts (chat, leaderboards, in-app purchase validation) may become separate services.
- **Server-authoritative.** The client sends intents only; this service validates and owns all state, timers and resources.
- **Resources computed on read:** `amount = stored + rate × (now − lastUpdated)`, persisted only when changed — no per-tick jobs.
- **Durable scheduled-event queue** (build completions, march arrivals, battles) processed in strict time order per world, in `BannersOfSham.Worker`.
- **Optimistic concurrency** (EF row versions) on cities and armies.
- **World map spatial queries** ("tiles in viewport", "nearest camp") need a grid index or PostGIS, not plain CRUD.
- **Shared contracts:** `BannersOfSham.Contracts` targets `netstandard2.1` (the newest .NET Unity supports) and holds DTOs, enums and game constants. Its DLL is copied into `Unity/BannersOfSham/Assets/Plugins/`.

## Open items before scaffolding

- Mobile login: guest/device-ID plus Google and Apple sign-in with account linking — Identity likely needs extending.
- Token lifetime / refresh tokens for long mobile sessions — see `Doc/SECURITY_AUDIT_PENDING_DECISIONS.md`.

## Core domain (to design)

World, City, Building, Troop, March, ScheduledEvent.
