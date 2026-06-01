# Architecture

## Local MVP

```text
Browser
  ↓
Angular frontend
  ↓ /api through proxy.conf.json
ASP.NET Core API
  ↓
In-memory metrics store
```

## Target deployment

```text
Cloudflare DNS
  ↓
Hetzner VPS
  ↓
Caddy reverse proxy
  ├── iamhakim.com       -> Angular frontend
  └── api.iamhakim.com   -> ASP.NET Core API
```

## API contract

```text
GET  /api/health  -> API/storage/bot status + uptime
GET  /api/stats   -> visits + UP clicks
POST /api/visit   -> increments page visits
POST /api/up      -> increments UP clicks
GET  /api/flow    -> explains the request flow
```

## Persistence roadmap

The first MVP uses an in-memory store to keep the feedback loop fast.
The next technical step is to replace `SiteMetricsStore` with PostgreSQL.
```
