# Nasheed Service — API Endpoints

**Base URL:** `http://localhost:5009`  
**Auth:** All business endpoints require `Authorization: Bearer <token>`.  
`x-tenant-id` should be sent by clients for tenant-aware routing, but this service also runs with configured single-tenant fallback (`MultiTenancy:TenantId`).  
**Last Updated:** May 7, 2026

---

## Artists

### `POST /api/artists`

Create a new artist.

**Request body:**

```json
{ "name": "string", "imageFileId": 123 }
```

**Response:** `201 Created` → `ArtistDto`

---

### `GET /api/artists/{id}`

Get a single artist by ID.

**Response:** `200 OK` → `ArtistDto` | `404 Not Found`

---

### `GET /api/artists?textFilter=&pageNumber=1&pageSize=10`

Get paginated list of artists.

**Response:** `200 OK` → `PaginatedList<ArtistDto>`

```json
{
  "items": [{ "id": 1, "name": "string", "imageFileId": null, "songCount": 0 }],
  "totalCount": 100,
  "pageNumber": 1,
  "pageSize": 20
}
```

---

### `PUT /api/artists/{id}`

Update an artist.

**Request body:**

```json
{ "name": "string", "imageFileId": 123 }
```

**Response:** `200 OK` → `ArtistDto`

---

### `DELETE /api/artists/{id}`

Delete an artist and **all its songs** (cascade).

For each song owned by the artist, the full song cascade runs first (see `DELETE /api/songs/{id}`).

**Response:** `200 OK`

---

## Songs

### `POST /api/songs`

Create a new song (triggers ingestion pipeline).

**Request body:**

```json
{
  "artistId": 1,
  "title": "string",
  "fileId": 456,
  "copyrightRiskLevel": "low",
  "contentSafetyFlag": "safe",
  "riskReason": null
}
```

`artistId` is optional. Omit it or set it to `null` to create a song without an artist.

**Response:** `201 Created` → `SongDto`

> Creating a song automatically queues a `FullPipeline` ingestion job.

---

### `GET /api/songs/{id}`

Get a single song by ID.

**Response:** `200 OK` → `SongDto` | `404 Not Found`

---

### `GET /api/songs?textFilter=&artistId=&state=&copyrightRiskLevel=&contentSafetyFlag=&pageNumber=1&pageSize=10`

Get paginated list of songs with optional filters.

**Response:** `200 OK` → `PaginatedList<SongDto>`

---

### `PUT /api/songs/{id}`

Update song metadata allowed by command contract.

**Request body:**

```json
{
  "title": "string",
  "artistId": 1,
  "durationSeconds": 240,
  "languageCode": "ar",
  "lyricsRaw": "[00:01.00]Raw line",
  "lyricsVerifiedLrc": "[00:01.00]Verified line\\n[00:02.00]Next",
  "lyricsPlainText": "Verified line\\nNext",
  "summary": "string",
  "vocalStyle": "string",
  "copyrightRiskLevel": "medium",
  "contentSafetyFlag": "flagged",
  "riskReason": "سبب التحقق"
}
```

**Response:** `200 OK` → `SongDto`

> If title, lyrics, summary, language, vocal style, duration, or legal compliance values change, an `EmbeddingGeneration` job is queued automatically.
> `artistId` change is rejected by handler logic.

---

### `DELETE /api/songs/{id}`

Delete a song and **all related data** (cascade).

The following are removed in order before the song row is deleted:

1. `SongMoodTagEntity` rows for the song
2. `SongIngestionJobEntity` rows for the song (all statuses)
3. `SongSearchDocumentEntity` for the song
4. `FavoriteEntity` rows for the song
5. `RatingEntity` rows for the song
6. `PlayLogEntity` rows for the song

After deletion the parent artist's `SongCount` is decremented when `artistId` exists; if `artistId` is null, artist update is skipped.

**Response:** `200 OK`

---

### `GET /api/songs/{id}/analysis`

Get the current processing status of a song (state + search index status).

**Response:** `200 OK` → `SongDto` | `404 Not Found`

---

### `GET /api/songs/{id}/similar?topN=10`

Get semantically similar songs using cosine similarity on embeddings.

**Query params:** `topN` (default 10)  
**Response:** `200 OK` → `List<SearchResultDto>`

```json
[{ "songId": 1, "title": "string", "artistName": "string", "score": 0.95 }]
```

---

## Ingestion Jobs

### `GET /api/ingestion/{id}`

Get a single ingestion job by ID.

**Response:** `200 OK` → `IngestionJobDto` | `404 Not Found`

---

### `GET /api/ingestion?songId=&status=&pageNumber=1&pageSize=10`

Get paginated ingestion job list with optional filters.

**Response:** `200 OK` → `PaginatedList<IngestionJobDto>`

---

### `POST /api/ingestion/{id}/retry`

Reset a job to `Pending` so the worker can pick it up again.

**Response:** `200 OK` → `IngestionJobDto`

> `RetryCount` is not reset by retry; `ResetForRetry()` clears `LastError` and `NextRetryAt`.

---

### `DELETE /api/ingestion/{id}`

Hard delete an ingestion job row.

**Response:** `200 OK` → `true`

---

### `POST /api/ingestion/songs/{songId}/reindex`

Queue a new `EmbeddingGeneration` job to re-embed a song.

**Response:** `200 OK` → `IngestionJobDto`

---

## Semantic Search

### `GET /api/search?q=&topN=10`

Search songs by natural language query using semantic similarity.

**Query params:** `q` (preferred), `query` (legacy alias), `topN` (default 10)  
**Response:** `200 OK` → `List<SearchResultDto>`

The endpoint first performs a fast lexical match on stored search text. If direct text matches are found, results are returned immediately without an embedding call. Otherwise, the query is embedded using `nasheed:embedding:settings` and ranked with PostgreSQL `pgvector` similarity. If `q/query` is empty, the endpoint returns an empty list.

---

## Interactions

### `POST /api/songs/{songId}/favorites`

Add a song to a user's favorites.

**Request body:**

```json
{ "userId": 123 }
```

**Response:** `200 OK` → `FavoriteDto`

---

### `DELETE /api/songs/{songId}/favorites`

Remove a song from a user's favorites.

**Request body:**

```json
{ "userId": 123 }
```

**Response:** `200 OK`

---

### `POST /api/songs/{songId}/ratings`

Rate a song (1–5). Creates or updates the user's rating for that song.

**Request body:**

```json
{ "userId": 123, "value": 4 }
```

**Response:** `200 OK` → `RatingDto`

---

### `POST /api/songs/{songId}/play`

Log a play event for a user.

**Request body:**

```json
{ "userId": 123 }
```

**Response:** `200 OK`

---

## Generation

### `POST /api/generation/lyrics`

Generate new nasheed lyrics using AI based on a theme/prompt.

**Request body:**

```json
{ "theme": "string", "languageCode": "ar", "style": "string" }
```

**Response:** `200 OK` → `GenerateLyricsResponseDto`

```json
{ "generatedLyrics": "string", "theme": "string", "style": "string" }
```

---

## DTOs Reference

### `ArtistDto`

```json
{ "id": 1, "name": "string", "imageFileId": null, "songCount": 0 }
```

### `SongDto`

```json
{
  "id": 1,
  "artistId": 1,
  "title": "string",
  "fileId": 456,
  "durationSeconds": 180,
  "languageCode": "ar",
  "lyricsRaw": null,
  "lyricsVerifiedLrc": null,
  "lyricsPlainText": null,
  "summary": null,
  "vocalStyle": null,
  "songState": "Done",
  "searchIndexStatus": "NotIndexed",
  "publishedAt": null,
  "moodTags": [],
  "created": "2026-05-02T10:00:00Z",
  "lastModified": null
}
```

`artistId` may be `null` for songs without an artist.

### `IngestionJobDto`

```json
{
  "id": 1,
  "songId": 1,
  "fileId": 456,
  "jobType": "FullPipeline",
  "jobStatus": "Completed",
  "retryCount": 0,
  "maxRetries": 3,
  "lastError": null,
  "nextRetryAt": null,
  "startedAt": "2026-05-02T10:00:00Z",
  "completedAt": "2026-05-02T10:02:00Z",
  "removedAt": null,
  "created": "2026-05-02T10:00:00Z",
  "lastModified": "2026-05-02T10:02:00Z"
}
```

### `SearchResultDto`

```json
{ "songId": 1, "title": "string", "artistName": "string", "score": 0.95 }
```
