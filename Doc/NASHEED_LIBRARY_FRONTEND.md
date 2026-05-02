# Nasheed Library Frontend

**Purpose:** Frontend architecture and implementation plan for the nasheed library experience in the Angular workspace.  
**Last Updated:** May 2, 2026  
**Status:** ⚠️ Proposed Design

---

## Overview

This document defines the frontend side of the nasheed library feature. The goal is to add upload, editing, playback, lyric synchronization, and discovery flows without breaking the established Angular and Nx patterns already used in the workspace.

The frontend is responsible for preparing audio before submit, providing playback and lyric experiences, and surfacing ingestion progress from the backend. It should integrate with existing translation, API, and real-time patterns rather than introducing a separate frontend architecture.

---

## Locked Frontend Decisions

The following decisions are fixed for the frontend implementation:

- Audio editing belongs on the frontend side
- The backend receives `FileId` after upload, not raw audio bytes
- `WaveSurfer.js` should be used for audio playback, trimming, and editing workflow
- The Angular workspace remains signals-first
- Existing SignalR support should be reused for live ingestion updates

---

## Reuse From Existing Platform

### Existing Frontend Capabilities To Reuse

The current frontend workspace already provides several patterns that should be reused:

- Signals-first state management style
- Existing API service patterns in Angular
- Existing translation discipline and Nx project structure
- Existing SignalR dependency for real-time updates

The nasheed UI should be implemented as a normal Angular feature within the current workspace structure, following the same translation and component usage patterns already established in the admin application.

### Existing Backend Capabilities The Frontend Should Rely On

The frontend should integrate with the backend platform instead of duplicating its responsibilities:

- FileManager for audio upload and file identity
- Nasheed backend endpoints for artists, songs, ingestion, ratings, favorites, play logs, and search
- Notification style real-time events for ingestion progress

The UI should treat upload, processing, and indexing as separate stages and should not try to perform backend analysis locally in the browser.

---

## Upload And Editing Flow

### Recommended Flow

1. User selects or drops an audio file in the admin upload screen
2. Frontend loads the file into a `WaveSurfer.js` editor experience
3. User previews, trims, and prepares the final audio version
4. Frontend uploads the prepared file through FileManager
5. FileManager returns a `FileId`
6. Frontend submits the nasheed record to the backend with `fileId` and `title`
7. Frontend transitions the item into an ingestion-monitoring state
8. Frontend listens for progress updates until analysis is complete

### Why The Frontend Owns Editing

This decision keeps the backend focused on durable storage, orchestration, and AI processing. It also aligns with the current feature direction that the backend should receive only a `FileId` and not large raw audio payloads for editing.

---

## Playback And Lyrics Experience

### Required Capabilities

The frontend must add the following capabilities:

- Audio upload interface
- Frontend audio editor flow before final submit
- Playback UI for nasheeds
- LRC parser and lyric synchronization service
- Search and discovery pages
- `WaveSurfer.js` integration for playback and editing

### Lyrics Synchronization Direction

The backend stores verified LRC and plain-text lyrics. The frontend should:

- parse LRC content into timestamped lyric entries
- synchronize active lyric highlighting with playback position
- gracefully fall back to plain-text lyrics when synchronized data is unavailable

The LRC parser and synchronization logic do not currently exist in the frontend workspace and should be introduced as dedicated feature code rather than embedded ad hoc into components.

---

## Real-Time Progress Handling

### Recommended Events To Surface In The UI

- Song uploaded
- AI extraction started
- Metadata extracted
- Lyrics verified
- Search index completed
- Generation request completed

### UI Expectations

The upload and management screens should show ingestion status clearly. The user should be able to distinguish between queued, running, failed, and complete states without refreshing the page.

If live progress is available through SignalR, the UI should subscribe to it. If live delivery is temporarily unavailable, the same screens should still support status refresh using the analysis-status or ingestion endpoints.

---

## Search And Discovery Experience

### Semantic Search Contract

The backend search flow is expected to build semantic documents from:

- Title
- Artist name
- Mood tags
- Vocal style
- Summary
- Verified lyrics with timestamps removed

The frontend should treat search as a semantic retrieval feature, not a simple local filter. The UI should support query entry, ranked result display, and navigation into song detail and playback views.

### Discovery Surfaces

Recommended frontend surfaces:

- Admin upload and ingestion management page
- Song detail page with playback and lyrics
- Search page for discovery and similarity results
- Reusable player area or player component that can be embedded in multiple views

---

## API Expectations From The Frontend Perspective

The frontend will depend on the following backend endpoint groups:

### Catalog Endpoints

- `POST /api/songs` using `fileId` and `title`
- `GET /api/songs`
- `GET /api/songs/{id}`
- `PUT /api/songs/{id}`
- `DELETE /api/songs/{id}`
- artist catalog endpoints as needed for authoring and browsing

### Ingestion Endpoints

- `GET /api/ingestion/jobs`
- `GET /api/ingestion/jobs/{id}`
- `POST /api/ingestion/jobs/{id}/retry`
- `POST /api/ingestion/jobs/{id}/remove`
- `POST /api/songs/{id}/reindex`
- `GET /api/songs/{id}/analysis-status`

### Interaction Endpoints

- `POST /api/songs/{id}/favorite`
- `DELETE /api/songs/{id}/favorite`
- `POST /api/songs/{id}/rating`
- `POST /api/songs/{id}/play-log`

### Search And Generation Endpoints

- `GET /api/search?query=...`
- `GET /api/search/similar/{songId}`
- `POST /api/generation/lyrics`

All business requests for this feature are tenant-scoped, so the frontend must continue supplying tenant context according to the platform's existing API conventions.

---

## Recommended Frontend Scope Order

1. Admin upload and ingestion status screen
2. Frontend audio editor using `WaveSurfer.js`
3. Basic audio playback with unsynchronized lyrics
4. LRC parsing and synchronized lyric highlighting
5. Semantic search experience

This order keeps the first milestone focused on getting content into the system and making backend progress visible before investing in the richer playback and discovery experience.

---

## Risks And Design Notes

### Audio Editing Complexity

Audio editing can quickly become larger than the initial feature scope. The first iteration should keep trimming and preview capabilities focused and avoid trying to build a full digital audio workstation experience.

### Dependency Introduction

`WaveSurfer.js` is not currently part of the workspace. Its integration should be kept inside a well-defined feature boundary so it does not leak implementation details into unrelated shared libraries.

### LRC Quality

Even with backend verification, some tracks may have imperfect timing or formatting. The UI should tolerate malformed or incomplete LRC content and fall back to plain-text rendering when needed.

### Real-Time Reliability

Live updates improve the ingestion workflow, but the screens still need a polling or manual refresh fallback so the feature remains usable during transient connection issues.

---

## Final Recommendation

Implement the nasheed frontend as a focused Angular feature that plugs into existing workspace patterns and treats the backend as the source of truth for ingestion, metadata, and search.

### Keep

- Signals-first Angular patterns
- Existing translation discipline
- Existing SignalR integration style
- FileManager-backed upload flow

### Add

- Admin upload and monitoring UI
- `WaveSurfer.js`-based audio editor
- Playback UI with lyric rendering
- LRC parsing and synchronization service
- Semantic search and discovery screens

### Avoid

- Sending raw audio editing workloads to the backend
- Building a separate frontend state model just for this feature
- Coupling UI logic directly to AI provider behavior

This approach keeps the frontend aligned with the platform while still covering the parts of the feature that are genuinely browser-owned.