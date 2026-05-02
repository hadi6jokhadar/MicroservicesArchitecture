# Nasheed Service — API Endpoints

**Base URL:** `http://localhost:5009`  
**Auth:** All endpoints require `Authorization: Bearer <token>` and `x-tenant-id: <tenantId>` headers.  
**Last Updated:** May 2, 2026

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

### `GET /api/artists?page=1&pageSize=20&search=`

Get paginated list of artists.

**Response:** `200 OK` → `PaginatedList<ArtistDto>`

```json
{
  "items": [{ "id": 1, "name": "string", "imageFileId": null, "songCount": 0 }],
  "totalCount": 100,
  "page": 1,
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

Delete an artist.

**Response:** `204 No Content`

---

## Songs

### `POST /api/songs`

Create a new song (triggers ingestion pipeline).

**Request body:**

```json
{ "artistId": 1, "title": "string", "fileId": 456 }
```

**Response:** `201 Created` → `SongDto`

> Creating a song automatically queues a `FullPipeline` ingestion job.

---

### `GET /api/songs/{id}`

Get a single song by ID.

**Response:** `200 OK` → `SongDto` | `404 Not Found`

---

### `GET /api/songs?page=1&pageSize=20&artistId=&state=`

Get paginated list of songs with optional filters.

**Response:** `200 OK` → `PaginatedList<SongDto>`

---

### `PUT /api/songs/{id}`

Update song title.

**Request body:**

```json
{ "title": "string" }
```

**Response:** `200 OK` → `SongDto`

---

### `DELETE /api/songs/{id}`

Delete a song.

**Response:** `204 No Content`

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

### `GET /api/ingestion?page=1&pageSize=20&status=&songId=`

Get paginated ingestion job list with optional filters.

**Response:** `200 OK` → `PaginatedList<IngestionJobDto>`

---

### `POST /api/ingestion/{id}/retry`

Reset a failed job to `Pending` so the worker picks it up again.

**Response:** `200 OK` → `IngestionJobDto`

---

### `DELETE /api/ingestion/{id}`

Mark an ingestion job as `Removed`.

**Response:** `204 No Content`

---

### `POST /api/ingestion/songs/{songId}/reindex`

Queue a new `EmbeddingGeneration` job to re-embed a song.

**Response:** `200 OK` → `IngestionJobDto`

---

## Semantic Search

### `GET /api/search?q=&topN=10`

Search songs by natural language query using semantic similarity.

**Query params:** `q` (required), `topN` (default 10)  
**Response:** `200 OK` → `List<SearchResultDto>`

The query is embedded using `nasheed:embedding:settings` and compared against stored embeddings using cosine similarity.

---

## Interactions

### `POST /api/songs/{songId}/favorites`

Add a song to the current user's favorites.

**Response:** `200 OK` → `FavoriteDto`

---

### `DELETE /api/songs/{songId}/favorites`

Remove a song from the current user's favorites.

**Response:** `204 No Content`

---

### `POST /api/songs/{songId}/ratings`

Rate a song (1–5). Creates or updates the user's rating for that song.

**Request body:**

```json
{ "value": 4 }
```

**Response:** `200 OK` → `RatingDto`

---

### `POST /api/songs/{songId}/play`

Log a play event for the current user.

**Response:** `204 No Content`

---

## Generation

### `POST /api/generation/lyrics`

Generate new nasheed lyrics using AI based on a theme/prompt.

**Request body:**

```json
{ "theme": "string", "language": "ar", "style": "string" }
```

**Response:** `200 OK` → `GenerateLyricsResponseDto`

```json
{ "lyrics": "string" }
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
  "searchIndexStatus": "Indexed",
  "publishedAt": null,
  "createdAt": "2026-05-02T10:00:00Z"
}
```

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
  "completedAt": "2026-05-02T10:02:00Z"
}
```

### `SearchResultDto`

```json
{ "songId": 1, "title": "string", "artistName": "string", "score": 0.95 }
```
