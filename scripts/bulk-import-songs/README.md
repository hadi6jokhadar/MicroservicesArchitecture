# Bulk-import songs into Nasheed

One-off data-migration tool: takes a folder of existing `.mp3` files and creates a `Song` record for each one via the existing Nasheed/FileManager APIs, so the already-automated ingestion pipeline (AI extraction, ASR, embeddings — see `src/Apps/Nasheed/Doc/INGESTION_PIPELINE.md`) picks them up with no other manual step per file.

This is not a new service feature — it only calls existing endpoints. Nothing here is deployed or referenced by any service; it's a script you run by hand when you have a batch of files to import.

## What it does, per file

1. Uploads the file to FileManager (`POST /api/v1/filemanager/files`, `group=4` / `Project` — the same group the Nasheed admin UI uses for song uploads) → gets back a file `Id`.
2. Calls Nasheed's `POST /api/v1/songs` with `{ title: <filename without extension>, fileId, artistId: null }` → this creates the `Song` row and automatically queues the AI ingestion job (`SongIngestionJobEntity`), same as a normal upload through the admin UI.

Title comes straight from the filename (extension stripped). Artist is left `null` — set it later during manual review in the Nasheed admin UI.

## Requirements

- **Node.js 18+** (uses built-in `fetch`/`FormData`/`Blob` — no `npm install` needed).
- Run it **on the same machine that's hosting the backend services** (e.g. PC2 per `Doc/DOCKER_DEPLOYMENT_GUIDE.md`) — the script calls `localhost:5001` (Identity), `localhost:5005` (FileManager), and `localhost:5009` (Nasheed) directly. On a Docker deployment those three ports are bound to `127.0.0.1`, so they're only reachable from that host, not from another machine.
- An Identity account with the `Admin` or `SuperAdmin` role (needed for both the FileManager upload and the Nasheed song-create endpoint).
- The stack must already be up and healthy (`docker compose ps` all `Up`, `/health` checks passing).

## Usage

```bash
node bulk-import-songs.mjs \
  --source "/path/to/folder/of/mp3s" \
  --email admin@example.com \
  --password 'your-password' \
  --tenant-id your-tenant-id \
  --concurrency 5 \
  --limit 5 \
  --csv ./import-results.csv
```

The last three flags are optional (see table below) — the example includes them just to show the syntax; drop any you don't need rather than leaving them in brackets.

| Flag            | Required | Default               | Notes                                                                 |
| --------------- | -------- | --------------------- | ---------------------------------------------------------------------- |
| `--source`      | yes      | —                     | Folder containing the `.mp3` files (non-recursive; subfolders are skipped) |
| `--email`       | yes      | —                     | Identity login for an Admin/SuperAdmin account                         |
| `--password`    | yes      | —                     | Identity login password                                                |
| `--tenant-id`   | yes      | —                     | Sent as `x-tenant-id` on every call                                    |
| `--concurrency` | no       | `5`                   | How many files are uploaded/created in parallel                        |
| `--limit`       | no       | (all)                 | Only process the first N files — use this for a dry run first          |
| `--csv`         | no       | `./import-results.csv`| Where results are logged                                               |

**Recommended first run:** use `--limit 5` and check the songs actually show up (Nasheed admin UI → Ingestion, or `GET /api/v1/songs`) before running the full batch.

## Resuming after a partial failure

The script appends one row per file to the CSV (`fileName,status,fileId,songId,error`). If you re-run the exact same command (same `--csv` path), any filename already recorded as `success` is skipped — so a failed run can just be re-run as-is to retry only the files that failed.

## Troubleshooting

- **401 on login or upload** — the account needs `Admin`/`SuperAdmin`; a plain `User` role isn't enough for the FileManager upload's `group=4`/service-level behavior or for creating songs.
- **Connection refused** — you're not running this on the host machine, or the stack isn't up yet.
- **A song's `SongState` doesn't progress past `Uploaded`** — `NasheedIngestionWorker` polls every 10s; give it a minute, then check the service logs if it's still stuck (see `src/Apps/Nasheed/Doc/INGESTION_PIPELINE.md`).
