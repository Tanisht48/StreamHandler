# StreamHandler

A real-time stream health monitor. Add HTTP, HLS, RTSP, or YouTube stream URLs and get live status updates pushed to the browser via SignalR. Recurring background checks run every 2 minutes via Hangfire.

---

## Architecture

```
StreamHandler/
├── StreamHandler.sln
├── StreamHandler.API/          ← .NET 8 Web API
└── StreamHandler.UI/           ← Angular 21 standalone SPA
```

**Backend:** .NET 8 · ASP.NET Core · Entity Framework Core · Hangfire · SignalR · Npgsql  
**Frontend:** Angular 21 · @microsoft/signalr  
**Database:** In-memory (default) or PostgreSQL (opt-in)

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 8.0+ |
| Node.js | 18+ |
| FFmpeg (`ffprobe`) | Any recent (optional, for RTSP/YouTube) |
| PostgreSQL | 15+ (optional) |

Install FFmpeg on Windows:
```powershell
winget install Gyan.FFmpeg
# or
choco install ffmpeg
```

---

## Getting Started

### 1. Run the API

```bash
cd StreamHandler.API
dotnet run
# API available at http://localhost:5000
# Hangfire dashboard at http://localhost:5000/hangfire
```

### 2. Run the Angular UI

```bash
cd StreamHandler.UI
npm install
npx ng serve
# UI available at http://localhost:4200
```

---

## Configuration

`StreamHandler.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "StreamHandlerDb": ""
  },
  "StreamHealth": {
    "CheckIntervalMinutes": 2,
    "HttpTimeoutSeconds": 5,
    "FfprobePath": "ffprobe"
  }
}
```

### Database

| `StreamHandlerDb` value | Behavior |
|-------------------------|----------|
| `""` (empty) | In-memory database, seeds 3 demo streams on startup |
| Postgres DSN | Connects to PostgreSQL, runs `MigrateAsync()` automatically |

**Switch to PostgreSQL:**

```bash
docker run --name streamhandler-pg \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 -d postgres
```

Set in `appsettings.json`:
```json
"StreamHandlerDb": "Host=localhost;Port=5432;Database=streamhandler;Username=postgres;Password=postgres"
```

### FFmpeg

- RTSP and YouTube streams are probed via `ffprobe` (spawned as a child process).
- Exit code 0 → **Live**, non-zero → **Offline**.
- If `ffprobe` is not on `PATH`, set `StreamHealth:FfprobePath` to the full binary path.
- If `ffprobe` is not installed at all, those streams degrade gracefully to **Unknown**.

---

## API Reference

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/streams` | Add a stream (triggers an immediate health check) |
| `GET` | `/streams` | List all streams (Live-first ordering) |
| `GET` | `/streams/{id}` | Get a single stream |
| `DELETE` | `/streams/{id}` | Remove a stream |
| `POST` | `/streams/{id}/check` | Manual health check (blocks until complete) |
| `GET` | `/status` | Summary counts (total / live / offline / checking / unknown) |
| `GET` | `/hangfire` | Hangfire recurring-job dashboard |

### Add a stream

```http
POST http://localhost:5000/streams
Content-Type: application/json

{
  "url": "https://stream.radioparadise.com/aac-128",
  "name": "Radio Paradise"
}
```

`name` is optional. `url` is required.

### Stream status values

| Status | Meaning |
|--------|---------|
| `Live` | Last check succeeded |
| `Offline` | Last check failed |
| `Checking` | Health check in progress |
| `Unknown` | Never checked, or ffprobe unavailable |

### Detected formats

`HLS` · `RTSP` · `YouTube` · `HTTP` · `Unknown`

---

## Real-time Updates (SignalR)

Connect to the hub at `http://localhost:5000/hubs/stream-status`.

| Event | Payload | Fired when |
|-------|---------|------------|
| `StreamStatusUpdated` | `{ id, name, url, format, status, lastCheckedAt, lastSeenLiveAt }` | After every health check for a stream |
| `StatusSummaryUpdated` | `{ totalStreams, live, offline, checking, unknown, lastHealthCheckRanAt }` | After every health-check batch |

---

## Project Structure

### API

```
StreamHandler.API/
├── Controllers/
│   ├── StreamsController.cs      ← CRUD + manual check
│   └── StatusController.cs       ← Summary counts
├── Data/
│   ├── StreamDbContext.cs
│   └── StreamDbContextFactory.cs ← dotnet-ef design-time factory
├── Hubs/
│   └── StreamStatusHub.cs        ← SignalR hub
├── Jobs/
│   └── StreamHealthJob.cs        ← Hangfire recurring job (every 2 min)
├── Migrations/
│   └── 20260415151716_InitialCreate.*
├── Models/
│   ├── Stream.cs                  ← Entity + StreamFormat / StreamStatus enums
│   ├── AddStreamRequest.cs
│   └── StreamStatusResponse.cs
├── Services/
│   ├── StreamFormatDetector.cs    ← URL-sniffing → format
│   ├── StreamHealthService.cs     ← HTTP HEAD + ffprobe + SignalR push
│   └── StreamService.cs           ← CRUD + immediate check on add
├── Program.cs
└── appsettings.json
```

### UI

```
StreamHandler.UI/src/app/
├── models/
│   └── stream.model.ts            ← Stream, StatusSummary, AddStreamRequest
├── services/
│   ├── stream.service.ts          ← HTTP client
│   └── signalr.service.ts         ← Hub connection + event dispatch
├── components/
│   ├── status-card/               ← Live/Offline/Total counts + SignalR indicator
│   ├── stream-list/               ← Table with per-row check/delete actions
│   └── add-stream/                ← Modal form
├── app.ts / app.html / app.scss   ← Root component + shell layout
├── app.config.ts
└── app.routes.ts
```

---

## Seeded Demo Streams (In-Memory mode)

| Name | URL |
|------|-----|
| BBC World Service | `http://stream.live.vc.bbcmedia.co.uk/bbc_world_service` |
| Dutch Radio 1 | `http://icecast.omroep.nl/radio1-bb-mp3` |
| Radio Paradise | `https://stream.radioparadise.com/aac-128` |

---

## Potential Next Features

- Stream detail route — status-change history over time
- Live/offline ratio chart (requires a status-history table)
- Notifications when a live stream goes offline
- Embedded HLS player for HTTP/HLS streams
- Auth — API keys + Hangfire dashboard lock
- Docker Compose — single `docker-compose up` for API + PostgreSQL + UI