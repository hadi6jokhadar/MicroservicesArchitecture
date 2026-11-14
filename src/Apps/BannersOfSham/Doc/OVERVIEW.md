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
- **Up to 100,000 players per world (GDD §14a, 2026-11-13).** Size every per-world structure for that (map land about 1,600 × 1,600 tiles, ~400,000 estates, ~15,000 live connections at peak by estimate); load-test it before launch (`Doc/LOAD_TESTING_GUIDE.md`). About 1,000,000 per world would need the world split by map region; not planned.
- **One game service, modular monolith.** City, buildings, troops, marches, combat and the world map change together transactionally, so they live in this one service. Only independent contexts (chat, leaderboards, in-app purchase validation) may become separate services.
- **Server-authoritative.** The client sends intents only; this service validates and owns all state, timers and resources.
- **Resources computed on read:** `amount = stored + rate × (now − lastUpdated)`, persisted only when changed — no per-tick jobs.
- **Durable scheduled-event queue** (build completions, march arrivals, battles) processed in strict time order **per target** (village, estate, checkpoint, Bandit village), not one serial queue per world, so events on different targets run in parallel; in `BannersOfSham.Worker`. One queue per world cannot keep up with 100,000 players.
- **Optimistic concurrency** (EF row versions) on cities and armies.
- **World map spatial queries** ("tiles in viewport", "nearest camp") need a grid index or PostGIS, not plain CRUD.
- **World grid (GDD §7):** a fixed grid whose size is chosen before launch for the player capacity (starting value 801 × 801, −400…+400; it can be larger), never resized once a world starts. **Spawn and zones (GDD §7.3, §14a, game concept of 2026-11-14):** in a world's first season the server places each new village near its quarter's frontier round the land's centre (fast "free tile near radius r in quarter q" queries); from the first restart the map has four nested **zones** (Green, Yellow, Orange, Red) by village points, redrawn at every restart, with attack rules by zone. Needs: "villages within radius r of a point" queries (a Bandit village's strength = the average points of the strongest 5 player villages within 10 tiles), a **season-end batch job** that sorts every player, redraws the zones, respawns the arena (Orange, Red: estates and checkpoints released, armies wiped 50% / 100%; Red at most the top 2% of active players, Orange the next 8%) and creates the Bandit villages and unclaimed Bandit estates again in each zone, and season state per world (Era, banner holder and its 7-day count). All rules are data in `game-data/world.json` (`zones`, `season`, `help`, `bandit_spawn`). Each tile stores terrain type and region ID; each region has a lock flag; tiles are stored and served in 16 × 16 chunks. Villages, estates, checkpoints and armies are stored separately from terrain (abandoned estates, ضيعة مهجورة, are the game's oases; there are no separate oases). Estate and checkpoint slots per player come from the Wali's House / Palace and Emir's House levels (GDD §7.8-7.9) — enforce server-side. Fogged (locked) tiles can't be settled, attacked, scouted or crossed — enforce this server-side.
- **Content-agnostic (GDD §2):** factions, units, buildings and regions are loaded from data packs (`Unity/BannersOfSham/game-data/` is the first); no content ids in code. Opening a region = clearing its lock flag in all worlds and enabling its faction.
- **Accounts are shared, progress is per world** (GDD §11): game state is scoped by `WorldId`; the Identity account is global.
- **قطاع الطرق — Bandits (GDD §10):** an AI-only faction run by a server worker (camps guarding abandoned estates and treasures, mid-game raids, endgame assaults on Great Banner builders). Starting values: raid strength formula with a once-per-24-hours cap per village (GDD §10.2), three camp tiers with garrison, respawn and loot (GDD §10.4), Bandit unit stats as a percentage of the player factions' role averages (GDD §10.3). Villages under beginner protection (GDD §6) can't be targeted.
- **Shared contracts:** `BannersOfSham.Contracts` targets `netstandard2.1` (the newest .NET Unity supports) and holds DTOs, enums and game constants. Its DLL is copied into `Unity/BannersOfSham/Assets/Plugins/`.

## Open items before scaffolding

- Mobile login: per the GDD (§6), each account is tied to a unique phone number verified by **SMS OTP**, plus **device fingerprinting** against duplicate accounts and bots — Identity needs extending for this.
- Timer queues: the GDD (§2) specifies **Redis Sorted Sets** for army marches and build/training completion — use this as the scheduled-event queue.
- Token lifetime / refresh tokens for long mobile sessions — see `Doc/SECURITY_AUDIT_PENDING_DECISIONS.md`.

## Core domain (to design)

World, City, Building, Troop, March, ScheduledEvent.
