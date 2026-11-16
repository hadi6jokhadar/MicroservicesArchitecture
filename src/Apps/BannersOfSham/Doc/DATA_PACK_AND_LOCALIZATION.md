# Banners of Sham — Data Pack and Localization Plan

How the game's numbers and text reach the backend and the Unity client. Agreed with the user on 2026-11-16, after the design closed (GDD §13.2). **Nothing here is built yet:** it is the plan for the game-logic phase (GDD §13.3). Read it before scaffolding `BannersOfSham` or writing the first client screen that shows a number or a word.

Related: [OVERVIEW.md](OVERVIEW.md) (backend architecture), `Unity/BannersOfSham/Doc/OVERVIEW.md` (client rules), `Unity/BannersOfSham/Doc/GDD.md` §2 (content-agnostic engine), `Unity/BannersOfSham/game-data/README.md` (the data and its tools), `Doc/TRANSLATION_SERVICE_GUIDE.md` (the Translation service).

---

## 1. The rule: one owner per kind of data

Neither the backend nor the client invents numbers. **`Unity/BannersOfSham/game-data/` is the single source of truth** for every rule and number (schema-validated JSON, decided through the question series Q1-Q110 and checked with the simulators in `game-data/tools/`). The backend and the client read copies of it.

```
game-data/*.json ──► export.py builds one versioned DATA PACK
                         ├──► Backend loads it at startup and uses it for every rule (server-authoritative)
                         └──► Unity downloads the same pack at login, only to DISPLAY costs, times and stats
```

| Data | Owner (edited only here) | Read by |
| :--- | :--- | :--- |
| Rules and numbers: unit and building stats, costs, times, formulas' constants | `game-data/` JSON (the designer, checked with the simulators) | Backend and client, through the pack |
| Content names and descriptions (factions, units, buildings, items, ...) | `game-data/` JSON, already bilingual `{ "ar": ..., "en": ... }` | Translation service and the client bundle, both **generated** |
| Interface text (buttons, menus, tooltips) | Translation service, edited from the admin panel | Client |
| Player state: villages, troops, resources, marches, reports | Backend database only | Client, through the API |
| Per-world settings: start date, season, world speed | Backend admin (the World record) | Backend |
| Art and models | The Unity project, linked by content id | Client |

**Never** type a number into a Unity script, prefab or ScriptableObject and plan to "move it to the backend later": two copies drift apart, and the server must decide everything anyway.

---

## 2. The data pack

**What goes in:** everything the rules need from `game-data/`: `factions/`, `units/`, `buildings/`, `balance/units_stats.json`, `balance/buildings_stats.json`, `resources.json`, `village.json`, `economy.json`, `combat.json`, `world.json`, `animals.json`, `bandit_camps.json`, `hero.json`, `hero_items.json`, `enums.json`.

**What stays out:** `schema/` (used to validate the pack, not shipped), `tools/`, `art/`, and the art-prompt fields inside unit and building files (the client keeps only the model references it needs).

**Before the first pack:** anything the code needs that still lives only in the GDD moves to `game-data/` first. Known case: the **Artifact list and effects** (GDD §7.13) are only a GDD table today.

**How it is built:** `export.py` already validates everything (`--check`). It gains one more output: a single pack file with a **version** (a content hash plus a readable number). A pack that fails validation is never built.

**Backend:**
- Loads the pack at startup; no content id ever appears in code (GDD §2: adding a region means adding data and art, not code). `BannersOfSham.Contracts` holds DTOs and generic enums only, never content ids or numbers.
- **Server-authoritative:** the client sends intents ("upgrade this building", "send these troops"); the backend checks them against the pack and owns all state and timers.
- **Retuning = a new pack version, no code change** (GDD §13.2).

**Client:**
- At login it compares its pack version with the server's and downloads the new pack when they differ.
- Uses it only to display (costs, times, stats) and for display-only previews, e.g. a resource counter ticking with the same formula the server uses (`stored + rate × elapsed`). The server's answer always wins.

---

## 3. The simulators are the backend's tests

The Python tools already compute every rule. The C# code must give **the same results on the same inputs** (golden tests):

| Python | Rules to port and test against |
| :--- | :--- |
| `tools/combat_rules.py` | Casualty exponent, siege engines per level and levels lowered, unique building bonuses, loot (`loot`, Q105), the scouting duel (`scout_duel`, Q106) |
| `tools/battle_sim.py` (`resolve`, `selftest`) | The full battle order: traps, rams, attack and defense with the wall, casualties, catapults; the 20 selftest cases |
| `tools/economy_rules.py` | Production, storage, construction and training times, Armor House stats and costs (Q103), village points, upkeep |
| `tools/world_sim.py`, `tools/economy_sim.py` | Bandit yardstick (Q104), estates and animals, the economy checks |

Plan: a small tool writes input/expected-output cases from the Python functions to a JSON file; the C# test project replays them. A rule change then means: edit the JSON or the formula, re-run the simulators, regenerate the cases, and the C# tests show what moved.

---

## 4. Text and localization: keys from the first day

**No hardcoded text anywhere**, Arabic included, not even "for now": switching later means hunting every string on every screen and fixing right-to-left layout bugs on the way. This is the backend's existing rule (`LocalizationKeys`, `Doc/LOCALIZATION_GUIDE.md`) applied to the game and the client.

### Three kinds of text

| Kind | Key | Where the words live |
| :--- | :--- | :--- |
| **Content** (factions, units, buildings, resources, items, Artifacts, animals, camps) | **Generated from the content id:** `{kind}.{id}.{field}`, e.g. `unit.dam_ablaq_swordsmen.name`, `unit.dam_ablaq_swordsmen.traits`, `building.matmura.name`, `faction.jazira.identity` | `game-data/` (`ar` and `en`). `export.py` generates the translation entries; nobody types them twice |
| **Interface** | Chosen by the client developer: `ui.{screen}.{element}`, e.g. `ui.village.upgrade`, `ui.map.send_troops` | Translation service, edited from the admin panel |
| **Server messages** (errors, notifications, report lines) | A key plus values: `error.{area}.{reason}`, `notify.{event}`, `report.{type}.{line}`, e.g. `report.battle.won {troops: 120}` | Translation service. The backend sends keys and numbers, **never sentences** |

### Reports are data, not text
Battle, scouting and trade reports are stored as structured data (unit ids, counts, losses, loot, wall levels). The client builds the sentences in the player's language. Reports stay small and work in any language, including ones added later.

### Translation service (port 5006, already built)
- Keys are imported as **tenant-owned keys of the fixed tenant `BannersOfSham`** (`#BannersOfSham#` key prefix on import) so other apps never see them.
- Suggested categories: `game.content` (generated from `game-data/`, overwritten on every import), `game.ui`, `game.messages`. The client loads them with `GET /api/v1/translations/{language}?category=...`.
- Languages: **Arabic first, English second** (both already in `game-data/`).

### Client
- Ships with a **bundled copy** of the Arabic and English text, so the first launch works offline; updates from the Translation service by version, like the pack.
- A missing key shows the key itself in development builds, so gaps are visible.
- **Arabic rendering spike before building the interface:** Arabic letters must join and run right to left. TextMeshPro is believed to need a shaping plugin (e.g. RTLTMPro), and Unity 6's UI Toolkit has a newer text system that may handle it natively. Neither is verified: test both on a real screen (mixed Arabic, English and numbers) and pick the UI system with that result.

---

## 5. First tasks of the game-logic phase

1. Move the GDD-only content the code needs into `game-data/` (the Artifact list first).
2. `export.py`: build the versioned pack file and the generated `game.content` translation entries (`ar` and `en`).
3. Golden-case tool: Python inputs and outputs to JSON for the C# tests (§3).
4. Arabic rendering spike in Unity (§4).
5. Scaffold `BannersOfSham` (`new-service` agent, [OVERVIEW.md](OVERVIEW.md)) with the pack loader and the translation import.
6. Build feature by feature, **backend first, then the Unity screen that shows it**, as thin vertical slices in roughly this order: resources and buildings → training → research and Armor House → marches and combat (with loot and scouting) → estates and animals → checkpoints → hero → Bandits → culture and Artifacts → gold and silver → alliances (the full list is GDD §13.3).

---

## 6. Open decisions (settle when the phase starts)

| Decision | Recommendation |
| :--- | :--- |
| How the pack reaches the backend (`game-data/` lives in the Unity repo, the backend in another) | `export.py` writes one versioned pack file; a build step copies it to the backend, which loads it from a configured path. Alternative: host it in FileManager. Not inside the Contracts DLL (that ties data releases to code releases) |
| When a new pack applies to a running world | Each world records the pack version it runs; balance changes apply at the next season restart, with an explicit "hotfix" flag for urgent fixes that apply at once |
| UI system for Arabic text | Decided by the spike in §4 |
