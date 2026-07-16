# Architecture

## Overview

Single domain, single VPS. Caddy is the only thing exposed to the internet
(ports 80/443); everything else binds to `127.0.0.1` and is only reachable
through Caddy's reverse proxy.

```text
Browser
  ↓ HTTPS (iamhakim.com)
Caddy (TLS termination, routing, static file serving)
  ├── /api/*, /hubs/*  → reverse_proxy → ASP.NET Core API (127.0.0.1:5172)
  └── everything else  → file_server   → prerendered Angular build (/opt/iamhakim/web)
                                            ↓
                                          MySQL (127.0.0.1:3306)
```

Frontend and backend are served under the **same origin**, so there is no
CORS in production (the API's CORS policy only applies in Development, for
`ng serve` on `localhost:4200`). The Angular app calls relative paths
(`/api/...`, `/hubs/live`); Caddy is what decides whether that request is
static content or gets proxied to the API.

## How the frontend talks to the backend

- **Local dev**: Angular dev server on `:4200`, API on `:5172`.
  `frontend/proxy.conf.json` forwards `/api/*` and `/hubs/*` (WebSocket-aware)
  to `http://localhost:5172`.
- **Production**: both live under `iamhakim.com`. Caddy path-routes:
  `/api/*` and `/hubs/*` go to the API via `reverse_proxy`; everything else
  is served as static files (or falls back to the client-render shell for
  the few routes that aren't prerendered — see below).
- **REST**: `ApiService` (Angular) wraps every `/api/*` endpoint —
  stats, health, booking flow, address search, algo-run tracking.
- **Realtime**: `LiveConnectionService` opens a SignalR WebSocket to
  `/hubs/live`. The API pushes `statsUpdated` and `timelineEvent` messages
  to all connected clients whenever a visit, click, A* run, or booking
  happens — this is what drives the live counters and the `/status` page.

## Frontend rendering

Every public page (`:lang/*` — home, projects, about, flow, status, book,
privacy) is **prerendered at build time** (`RenderMode.Prerender` in
`app.routes.server.ts`), once per language (en/fr/nl). Caddy serves these
static files directly — there's no Node process rendering pages
per-request in production. `book/manage` is the one route that's
client-only (`RenderMode.Client`): it's a private, token-based page that's
disallowed in `robots.txt` and has no reason to be prerendered.

Legacy un-prefixed URLs (`/status`, `/projects`, etc., from before the
`en/fr/nl` locale prefix existed) and the bare root `/` are handled by
Caddy itself (`redir ... 301` in the Caddyfile) — not by Angular. This
matters: Angular's own `redirectTo` rules for these same paths
(`app.routes.ts`) exist as a fallback but never actually fire in
production, because Caddy intercepts and redirects before the request
ever reaches the static file layer.

## Backend

ASP.NET Core minimal API (`backend/IAmHakim.Api`, no controllers — routes
are mapped directly in `Program.cs`). Key pieces:

- **MySQL** via EF Core (`AppDbContext`), migrations applied automatically
  on startup (`MigrateAsync`). Tables: `SiteStats`, `SiteEvents`,
  `Bookings`.
- **SignalR hub** (`/hubs/live`) for realtime push.
- **Booking flow**: email-verification → pending request → admin
  accept/reject (a small server-rendered HTML admin UI, `AdminPage(...)`
  in `Program.cs` — not part of the Angular app) → calendar event created
  on acceptance (Google Calendar / Microsoft Graph / ICS / mock provider,
  selected via `Booking:Mode` + provider `Enabled` flags).
- **Anti-abuse**: Cloudflare Turnstile on the booking form, salted-IP-hash
  rate limiting (raw IPs are never stored — see `ClientIdentityService`).
- **Address search**: proxies Belgian address autocomplete through
  OpenStreetMap Nominatim (no API key needed).
- **Retention**: a background hosted service (`BookingRetentionService`)
  sweeps old booking rows per the privacy policy's retention promise.

## Deployment

Config lives in `deploy/` (Caddyfile, systemd unit for the API,
`DEPLOY.md` for a from-scratch VPS bootstrap). An automated script on the
VPS itself (webhook-triggered on push) handles ongoing deploys: builds
both projects, applies EF migrations, swaps the API binary, copies the
Angular `browser` build into `/opt/iamhakim/web`, regenerates and
validates the CSP, then reloads Caddy. See `deploy/DEPLOY.md` for the
full manual bootstrap and update commands.

`infra/` is not used — deployment files live in `deploy/`, not there.
