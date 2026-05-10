# Nasheed Service — Entities and Data Model

**Last Updated:** May 7, 2026

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

| Column        | Type   | Notes                        |
| ------------- | ------ | ---------------------------- |
| `Id`          | int    | PK                           |
| `Name`        | string | Required, max 200            |
| `ImageFileId` | int?   | FileManager file ID          |
| `SongCount`   | int    | Maintained by domain methods |

**Domain methods:** `Create(name, imageFileId?)`, `Update(name, imageFileId?)`, `IncrementSongCount()`, `DecrementSongCount()`

---

### `SongEntity`

| Column              | Type                            | Notes                                                    |
| ------------------- | ------------------------------- | -------------------------------------------------------- |
| `Id`                | int                             | PK                                                       |
| `ArtistId`          | int?                            | Optional FK → ArtistEntity                               |
| `Title`             | string                          | Required, max 300                                        |
| `FileId`            | int                             | FileManager file ID for the audio file                   |
| `DurationSeconds`   | int?                            | Duration in seconds                                      |
| `LanguageCode`      | string?                         | e.g. `"ar"`, `"en"`                                      |
| `LyricsRaw`         | string?                         | Raw LRC lyrics from enrichment response                  |
| `LyricsVerifiedLrc` | string?                         | LRC-formatted verified lyrics                            |
| `LyricsPlainText`   | string?                         | Plain text version of verified lyrics                    |
| `Summary`           | string?                         | AI-generated summary                                     |
| `VocalStyle`        | string?                         | AI-extracted style description                           |
| `LegalCompliance`   | `LegalComplianceEntity` (owned) | AI-seeded legal compliance metadata (EF Core owned type) |
| `SongState`         | `SongState`                     | Processing lifecycle state                               |
| `SearchIndexStatus` | `SearchIndexStatus`             | Embedding/index state                                    |
| `PublishedAt`       | DateTime?                       | When the song was published                              |

**Domain methods:** `Create(artistId?, title, fileId)`, `UpdateMetadata(languageCode?, lyricsRaw?, summary?, vocalStyle?, durationSeconds?)`, `UpdateLegalComplianceFromAi(copyrightRiskLevel?, contentSafetyFlag?, riskReason?)`, `SetVerifiedLyrics(lrc, plainText)`, `UpdateTitle(title)`, `UpdateArtist(artistId?)`, `SetState(SongState)`, `SetSearchIndexStatus(SearchIndexStatus)`, `Publish()`

`ArtistId` is optional. Songs can be created without linking an artist.

`UpdateMetadata` resets `LyricsVerifiedLrc` and `LyricsPlainText` when `LyricsRaw` is updated.

`UpdateLegalComplianceFromAi` accepts AI string values, normalizes casing, validates allowed values, and ignores invalid combinations instead of failing the ingestion job.

#### `LegalCompliance` fields (EF Core Owned Type)

`LegalCompliance` is configured as an **EF Core owned type** (`OwnsOne<LegalComplianceEntity>`) on `SongEntity`. Its columns are stored inline in the `Songs` table with the prefix `LegalCompliance_`.

| Column (in Songs table)              | Type    | Notes                             |
| ------------------------------------ | ------- | --------------------------------- |
| `LegalCompliance_CopyrightRiskLevel` | string? | Allowed: `low`, `medium`, `high`  |
| `LegalCompliance_ContentSafetyFlag`  | string? | Allowed: `safe`, `flagged`        |
| `LegalCompliance_RiskReason`         | string? | Nullable; can contain Arabic text |

> **Migration:** `20260507085946_RefactorLegalComplianceOwnedType` applied this change.

---

### `SongMoodTagEntity`

| Column   | Type   | Notes                                         |
| -------- | ------ | --------------------------------------------- |
| `Id`     | int    | PK                                            |
| `SongId` | int    | FK → SongEntity (cascade delete)              |
| `Tag`    | string | Mood tag value (e.g. `"calm"`, `"uplifting"`) |

> **EF config:** `DeleteBehavior.Cascade` is set on the `SongId` FK so mood tags are removed automatically when a song is deleted at the database level.

---

### `FavoriteEntity`

> **Note:** Does NOT extend `BaseEntity`. Has composite primary key `(UserId, SongId)`.

| Column      | Type     | Notes                                 |
| ----------- | -------- | ------------------------------------- |
| `UserId`    | int      | Part of composite PK                  |
| `SongId`    | int      | Part of composite PK, FK → SongEntity |
| `CreatedAt` | DateTime | UTC                                   |

**Domain method:** `Create(userId, songId)`

---

### `RatingEntity`

> **Note:** Does NOT extend `BaseEntity`. Has composite primary key `(UserId, SongId)`.

| Column      | Type      | Notes                                 |
| ----------- | --------- | ------------------------------------- |
| `UserId`    | int       | Part of composite PK                  |
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
| `UserId`   | int      | The user who played         |
| `PlayedAt` | DateTime | UTC timestamp of play event |

---

### `SongIngestionJobEntity`

Tracks each AI processing job with full retry state.

| Column        | Type                 | Notes                                      |
| ------------- | -------------------- | ------------------------------------------ |
| `Id`          | int                  | PK                                         |
| `SongId`      | int                  | FK → SongEntity                            |
| `FileId`      | int                  | Audio file ID (from FileManager)           |
| `JobType`     | `IngestionJobType`   | Which stage to run                         |
| `JobStatus`   | `IngestionJobStatus` | Current execution state                    |
| `RetryCount`  | int                  | How many times this job has been attempted |
| `MaxRetries`  | int                  | Default 3                                  |
| `LastError`   | string?              | Last error message                         |
| `NextRetryAt` | DateTime?            | When to retry after failure                |
| `StartedAt`   | DateTime?            | When the current run began                 |
| `CompletedAt` | DateTime?            | When successfully completed                |
| `RemovedAt`   | DateTime?            | When manually removed                      |

**Domain methods:** `Create(songId, fileId, jobType, maxRetries=3)`, `MarkRunning()`, `MarkCompleted()`, `MarkFailed(error, nextRetryAt, retryable=true)`, `MarkRemoved()`, `ResetForRetry()`

> **Unique index:** `(SongId, JobType)` has a unique index guarding against duplicate concurrent jobs for the same song+type pair. The worker checks for an existing pending/running job before inserting a new one.

`MarkFailed` behavior is important:

- increments `RetryCount`
- sets `JobStatus = Pending` while `retryable=true` and `RetryCount < MaxRetries`
- sets `JobStatus = Failed` when `retryable=false`
- sets `JobStatus = Failed` when `retryable=true` and `RetryCount >= MaxRetries`

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
  └── SongEntity (0..N, FK: ArtistId nullable)
        ├── SongMoodTagEntity (1:N)
        ├── PlayLogEntity (1:N)
        ├── FavoriteEntity (1:N, composite PK)
        ├── RatingEntity (1:N, composite PK)
        ├── SongIngestionJobEntity (1:N)
        └── SongSearchDocumentEntity (1:1, unique SongId)
```

### Cascade Deletion (Application-Level)

Cascade is enforced in the application layer (not via EF Core foreign-key cascade) to allow file-cleanup hooks and logging.

| Trigger           | Cascades to                                                                                                                                                                                                                                   |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Delete Artist** | Deletes all `SongEntity` rows via `GetByArtistIdAsync`, then runs the full song cascade for each                                                                                                                                              |
| **Delete Song**   | Deletes `SongMoodTagEntity`, `SongIngestionJobEntity`, `SongSearchDocumentEntity`, `FavoriteEntity`, `RatingEntity`, `PlayLogEntity` (in that order) before removing the song row. If `ArtistId` is null, artist song-count update is skipped |

---

## EF Core Configuration Notes

- All entity configurations are in `Nasheed.Infrastructure/Persistence/Configurations/`
- `FavoriteEntity` and `RatingEntity` use `modelBuilder.Entity<T>().HasKey(e => new { e.UserId, e.SongId })`
- `EmbeddingJson` is mapped as `text` and cast to `vector` in search SQL
- `SongEntity.LegalCompliance` is configured as `OwnsOne<LegalComplianceEntity>()` — columns stored inline in the `Songs` table
- `SongEntity.ArtistId` is nullable (`IsRequired(false)`) with `DeleteBehavior.Restrict`
- `SongMoodTagEntity` FK has `DeleteBehavior.Cascade` — tags are removed at DB level when a song is deleted
- `SongIngestionJobEntity` has a unique index on `(SongId, JobType)` — enforced at DB level to prevent duplicate concurrent jobs
- Migrations assembly: `Nasheed.Infrastructure`
- Migration command (from solution root):
  ```powershell
  dotnet ef migrations add <Name> --project src\Apps\Nasheed\Nasheed.Infrastructure --startup-project src\Apps\Nasheed\Nasheed.API
  ```
- When changing a column type from `string` to `int` in PostgreSQL, use an explicit `USING` cast in the migration SQL (for example `USING "ColumnName"::integer`) because automatic casting is not applied for these schema changes.

### Applied Migrations

| Migration Name                                    | Date       | Description                                                             |
| ------------------------------------------------- | ---------- | ----------------------------------------------------------------------- |
| `20260508153633_MakeSongArtistOptional`           | 2026-05-08 | Makes `Songs.ArtistId` nullable and keeps artist relation optional      |
| `20260507085946_RefactorLegalComplianceOwnedType` | 2026-05-07 | Converts `LegalCompliance` fields on `SongEntity` to EF Core owned type |
