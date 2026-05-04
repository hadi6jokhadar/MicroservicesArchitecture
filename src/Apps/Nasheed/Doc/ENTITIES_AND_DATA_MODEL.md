# Nasheed Service — Entities and Data Model

**Last Updated:** May 4, 2026

---

## Enums

### `SongState`

Tracks the lifecycle of a song through processing.

| Value      | Int | Meaning                           |
| ---------- | --- | --------------------------------- |
| `Uploaded` | 0   | File uploaded, not yet queued     |
| `InQueue`  | 1   | Queued for ingestion processing   |
| `Pending`  | 2   | Ingestion in progress             |
| `Done`     | 3   | Fully processed and indexed       |
| `Failed`   | 4   | Ingestion failed (may be retried) |

### `SearchIndexStatus`

Tracks whether a song has been embedded and indexed for semantic search.

| Value        | Int | Meaning                               |
| ------------ | --- | ------------------------------------- |
| `NotIndexed` | 0   | No embedding exists                   |
| `Indexing`   | 1   | Embedding generation in progress      |
| `Indexed`    | 2   | Embedding saved, available for search |
| `Failed`     | 3   | Embedding generation failed           |

### `IngestionJobType`

The type of work an ingestion job performs.

| Value                 | Int | Meaning                                 |
| --------------------- | --- | --------------------------------------- |
| `FullPipeline`        | 0   | Run single enrichment request in worker |
| `MetadataExtraction`  | 1   | Extract language, summary, vocal style  |
| `LyricsVerification`  | 2   | Verify and format LRC lyrics            |
| `EmbeddingGeneration` | 3   | Generate and store semantic embedding   |

### `IngestionJobStatus`

The execution state of an ingestion job.

| Value       | Int | Meaning                                  |
| ----------- | --- | ---------------------------------------- |
| `Pending`   | 0   | Waiting to be picked up by the worker    |
| `Running`   | 1   | Currently being processed                |
| `Completed` | 2   | Successfully finished                    |
| `Failed`    | 3   | Failed (may retry based on `RetryCount`) |
| `Removed`   | 4   | Manually removed                         |

---

## Entities

All entities extend `BaseEntity` (from `IhsanDev.Shared.Kernel`) which provides `Id` (int, auto-increment), `CreatedAt`, `UpdatedAt`, and soft-delete via `IsDeleted`. Exceptions are noted below.

### `ArtistEntity`

| Column        | Type    | Notes                        |
| ------------- | ------- | ---------------------------- |
| `Id`          | int     | PK                           |
| `Name`        | string  | Required, max 200            |
| `ImageFileId` | string? | FileManager file ID          |
| `SongCount`   | int     | Maintained by domain methods |

**Domain methods:** `Create(name, imageFileId?)`, `Update(name, imageFileId?)`, `IncrementSongCount()`, `DecrementSongCount()`

---

### `SongEntity`

| Column              | Type                | Notes                                   |
| ------------------- | ------------------- | --------------------------------------- |
| `Id`                | int                 | PK                                      |
| `ArtistId`          | int                 | FK → ArtistEntity                       |
| `Title`             | string              | Required, max 300                       |
| `FileId`            | string              | FileManager file ID for the audio file  |
| `DurationSeconds`   | int?                | Duration in seconds                     |
| `LanguageCode`      | string?             | e.g. `"ar"`, `"en"`                     |
| `LyricsRaw`         | string?             | Raw LRC lyrics from enrichment response |
| `LyricsVerifiedLrc` | string?             | LRC-formatted verified lyrics           |
| `LyricsPlainText`   | string?             | Plain text version of verified lyrics   |
| `Summary`           | string?             | AI-generated summary                    |
| `VocalStyle`        | string?             | AI-extracted style description          |
| `SongState`         | `SongState`         | Processing lifecycle state              |
| `SearchIndexStatus` | `SearchIndexStatus` | Embedding/index state                   |
| `PublishedAt`       | DateTime?           | When the song was published             |

**Domain methods:** `Create(artistId, title, fileId)`, `UpdateMetadata(languageCode?, lyricsRaw?, summary?, vocalStyle?, durationSeconds?)`, `SetVerifiedLyrics(lrc, plainText)`, `UpdateTitle(title)`, `SetState(SongState)`, `SetSearchIndexStatus(SearchIndexStatus)`, `Publish()`

`UpdateMetadata` resets `LyricsVerifiedLrc` and `LyricsPlainText` when `LyricsRaw` is updated.

---

### `SongMoodTagEntity`

| Column   | Type   | Notes                                         |
| -------- | ------ | --------------------------------------------- |
| `Id`     | int    | PK                                            |
| `SongId` | int    | FK → SongEntity                               |
| `Tag`    | string | Mood tag value (e.g. `"calm"`, `"uplifting"`) |

---

### `FavoriteEntity`

> **Note:** Does NOT extend `BaseEntity`. Has composite primary key `(UserId, SongId)`.

| Column      | Type     | Notes                                 |
| ----------- | -------- | ------------------------------------- |
| `UserId`    | string   | Part of composite PK                  |
| `SongId`    | int      | Part of composite PK, FK → SongEntity |
| `CreatedAt` | DateTime | UTC                                   |

**Domain method:** `Create(userId, songId)`

---

### `RatingEntity`

> **Note:** Does NOT extend `BaseEntity`. Has composite primary key `(UserId, SongId)`.

| Column      | Type      | Notes                                 |
| ----------- | --------- | ------------------------------------- |
| `UserId`    | string    | Part of composite PK                  |
| `SongId`    | int       | Part of composite PK, FK → SongEntity |
| `Value`     | int       | 1–5                                   |
| `CreatedAt` | DateTime  | UTC                                   |
| `UpdatedAt` | DateTime? | Set on update                         |

---

### `PlayLogEntity`

| Column     | Type     | Notes                       |
| ---------- | -------- | --------------------------- |
| `Id`       | int      | PK                          |
| `SongId`   | int      | FK → SongEntity             |
| `UserId`   | string   | The user who played         |
| `PlayedAt` | DateTime | UTC timestamp of play event |

---

### `SongIngestionJobEntity`

Tracks each AI processing job with full retry state.

| Column        | Type                 | Notes                                      |
| ------------- | -------------------- | ------------------------------------------ |
| `Id`          | int                  | PK                                         |
| `SongId`      | int                  | FK → SongEntity                            |
| `FileId`      | string               | Audio file ID (from FileManager)           |
| `JobType`     | `IngestionJobType`   | Which stage to run                         |
| `JobStatus`   | `IngestionJobStatus` | Current execution state                    |
| `RetryCount`  | int                  | How many times this job has been attempted |
| `MaxRetries`  | int                  | Default 3                                  |
| `LastError`   | string?              | Last error message                         |
| `NextRetryAt` | DateTime?            | When to retry after failure                |
| `StartedAt`   | DateTime?            | When the current run began                 |
| `CompletedAt` | DateTime?            | When successfully completed                |
| `RemovedAt`   | DateTime?            | When manually removed                      |

**Domain methods:** `Create(songId, fileId, jobType, maxRetries=3)`, `MarkRunning()`, `MarkCompleted()`, `MarkFailed(error, nextRetryAt)`, `MarkRemoved()`, `ResetForRetry()`

`MarkFailed` behavior is important:

- increments `RetryCount`
- sets `JobStatus = Pending` while `RetryCount < MaxRetries`
- sets `JobStatus = Failed` when `RetryCount >= MaxRetries`

---

### `SongSearchDocumentEntity`

Stores the text + embedding used for semantic search.

| Column              | Type     | Notes                                                    |
| ------------------- | -------- | -------------------------------------------------------- |
| `Id`                | int      | PK                                                       |
| `SongId`            | int      | FK → SongEntity (unique)                                 |
| `SearchText`        | string   | Combined text used to generate embedding                 |
| `EmbeddingJson`     | string   | Vector literal text cast to `vector` for pgvector search |
| `EmbeddingModelKey` | string   | AI settings key used to generate this embedding          |
| `EmbeddedAt`        | DateTime | UTC timestamp of embedding generation                    |
| `IndexVersion`      | int      | Increments on re-embedding                               |

**Domain methods:** `Create(songId, searchText, embeddingJson, embeddingModelKey)`, `Update(searchText, embeddingJson, embeddingModelKey)`

> **Search behavior:** Semantic search uses PostgreSQL `pgvector` operators on `EmbeddingJson::vector` for server-side top-N ranking. If `pgvector` is unavailable, repository logic falls back to in-memory cosine similarity.

---

## Relationships

```
ArtistEntity
  └── SongEntity (1:N, FK: ArtistId)
        ├── SongMoodTagEntity (1:N)
        ├── PlayLogEntity (1:N)
        ├── FavoriteEntity (1:N, composite PK)
        ├── RatingEntity (1:N, composite PK)
        ├── SongIngestionJobEntity (1:N)
        └── SongSearchDocumentEntity (1:1, unique SongId)
```

### Cascade Deletion (Application-Level)

Cascade is enforced in the application layer (not via EF Core foreign-key cascade) to allow file-cleanup hooks and logging.

| Trigger           | Cascades to                                                                                                                                                                       |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Delete Artist** | Deletes all `SongEntity` rows via `GetByArtistIdAsync`, then runs the full song cascade for each                                                                                  |
| **Delete Song**   | Deletes `SongMoodTagEntity`, `SongIngestionJobEntity`, `SongSearchDocumentEntity`, `FavoriteEntity`, `RatingEntity`, `PlayLogEntity` (in that order) before removing the song row |

---

## EF Core Configuration Notes

- All entity configurations are in `Nasheed.Infrastructure/Persistence/Configurations/`
- `FavoriteEntity` and `RatingEntity` use `modelBuilder.Entity<T>().HasKey(e => new { e.UserId, e.SongId })`
- `EmbeddingJson` is mapped as `text` and cast to `vector` in search SQL
- Migrations assembly: `Nasheed.Infrastructure`
- Migration command (from solution root):
  ```powershell
  dotnet ef migrations add <Name> --project src\Apps\Nasheed\Nasheed.Infrastructure --startup-project src\Apps\Nasheed\Nasheed.API
  ```
