# iamhakim

Personal platform for `iamhakim.com`.

This MVP is intentionally small but real:

- Angular frontend with a polished portfolio/product-style UI
- ASP.NET Core API
- MySQL persistence for visits, maze/A* runs, booking requests and events
- SignalR realtime channel for live counters and connection state
- Backend flow simulation page
- Status dashboard

## Structure

```text
iamhakim/
├── backend/
│   └── IAmHakim.Api/
├── frontend/
├── infra/
└── README.md
```

## Run locally

### 1. Backend

```powershell
cd backend/IAmHakim.Api
dotnet restore
dotnet run --launch-profile http
```

Backend URL:

```text
http://localhost:5172
```

Useful endpoints:

```text
GET  /api/health
GET  /api/stats
GET  /api/events
POST /api/visit
POST /api/up
POST /api/flow/simulate
GET  /hubs/live
```

The MySQL schema is created automatically through EF Core migrations. In production, set `ConnectionStrings__Default`, `Security__ClientIdentity__IpHashSalt`, and the Turnstile keys in the systemd unit.

### 2. Frontend

```powershell
cd frontend
npm install
npm start
```

Frontend URL:

```text
http://localhost:4200
```

Angular uses `proxy.conf.json` so calls to `/api` and `/hubs` are forwarded to the backend.

## What this version proves

This project is not just a static portfolio. It demonstrates a complete technical path:

```text
User click -> Angular service -> ASP.NET Core endpoint -> MySQL -> SignalR -> live UI update
```

## Next production steps

- Deploy with systemd + Caddy on Hetzner
- Point DNS to the VPS
- Wire live Google Calendar + Resend secrets for the approval-based booking flow
- Add bot status integration
