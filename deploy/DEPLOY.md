# Deployment - iamhakim - Ubuntu 24.04 VPS (Hetzner CX23)

Your server already has: **Node 22**, **MySQL 8**, git, build-essential.
Still to install: **.NET 10 SDK/runtime** and **Caddy**.

> Important: the project targets `net10.0`, but EF Core stays on 9.x because `Pomelo.EntityFrameworkCore.MySql 9.x` isn't compatible with EF Core 10.x. Don't bump `Microsoft.EntityFrameworkCore.Design` to 10.x until Pomelo 10 stable is out.
RootShell bot: unchanged, keeps running alongside.

---

## 0. Dedicated service user (isolation)

```bash
sudo useradd --system --create-home --shell /usr/sbin/nologin iamhakim
sudo mkdir -p /opt/iamhakim/api /opt/iamhakim/web
sudo chown -R iamhakim:iamhakim /opt/iamhakim
```

## 1. Dedicated MySQL database

RootShell keeps its own database. Create a separate database + user for iamhakim:

```bash
sudo mysql
```
```sql
CREATE DATABASE iamhakim CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'iamhakim'@'localhost' IDENTIFIED BY 'A_STRONG_PASSWORD';
GRANT ALL PRIVILEGES ON iamhakim.* TO 'iamhakim'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```
Carry this password over into `iamhakim-api.service` (the `ConnectionStrings__Default` variable).

## 2. Install .NET 10

```bash
# Microsoft feed (Ubuntu 24.04)
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/ms.deb
sudo dpkg -i /tmp/ms.deb
sudo apt update
sudo apt install -y dotnet-sdk-10.0
dotnet --version   # verify
```

## 3. Fetch the code and build

```bash
cd /tmp && git clone <your-repo> iamhakim   # or scp your zip
cd iamhakim

# --- API ---
cd backend/IAmHakim.Api
dotnet publish -c Release -o /opt/iamhakim/api
# applies migrations (creates the tables in the iamhakim database)
# (temporary connection string export for the EF command)
export ConnectionStrings__Default="server=localhost;port=3306;database=iamhakim;user=iamhakim;password=A_STRONG_PASSWORD;TreatTinyAsBoolean=false"
export Security__ClientIdentity__IpHashSalt="GENERATE_A_LONG_RANDOM_SECRET"
# Active Turnstile only after creating a widget in Cloudflare.
# export Security__Turnstile__Enabled=true
# export Security__Turnstile__SiteKey="0x4AAAAA_PUBLIC_SITE_KEY"
# export Security__Turnstile__SecretKey="0x4AAAAA_PRIVATE_SECRET_KEY"
dotnet tool install --global dotnet-ef --version 9.*   # if not already installed
~/.dotnet/tools/dotnet-ef database update --project .
#  (otherwise the app applies migrations itself on startup; doesn't hide a real migration error)

# --- WEB (Angular, prerendered static) ---
# Every public page is prerendered at build time (RenderMode.Prerender) -
# Caddy serves the output directly via file_server, no Node process involved.
# Only the browser bundle matters here, not the SSR server build.
cd ../../frontend
npm ci
npm run build
sudo mkdir -p /opt/iamhakim/web
sudo rm -rf /opt/iamhakim/web/*
sudo cp -r dist/frontend/browser/. /opt/iamhakim/web/
sudo chown -R iamhakim:iamhakim /opt/iamhakim
```

> Note: the app automatically applies EF migrations on startup
> (`MigrateAsync`), so the `database update` step is optional.

## 4. systemd - API

The API runs as `Type=simple` and only listens on `127.0.0.1:5172`, so it's not directly exposed to the internet. The frontend has no systemd service: it's static content served by Caddy.

```bash
sudo cp deploy/iamhakim-api.service /etc/systemd/system/
# EDIT iamhakim-api.service: set the real MySQL password
sudo nano /etc/systemd/system/iamhakim-api.service

sudo systemctl daemon-reload
sudo systemctl enable --now iamhakim-api
sudo systemctl status iamhakim-api   # should be "active (running)"
```

## 5. Caddy - reverse proxy + automatic HTTPS

```bash
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update && sudo apt install -y caddy

sudo cp deploy/Caddyfile /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Before reloading: **point the DNS** for `iamhakim.com` (and `www`) at your VPS's public IP
(A records). Caddy will obtain the certificate automatically on first access.

## 6. Firewall

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```
Port 5172 (API) stays **internal** (bound to 127.0.0.1) - not open. The frontend has no port of its own: Caddy serves it directly from disk.

---

## Updating later

In practice, updates go through the automated deploy script (webhook-triggered on push). For a manual update:

```bash
cd /tmp/iamhakim && git pull
cd backend/IAmHakim.Api && dotnet publish -c Release -o /opt/iamhakim/api
cd ../../frontend && npm ci && npm run build \
  && sudo rm -rf /opt/iamhakim/web/* \
  && sudo cp -r dist/frontend/browser/. /opt/iamhakim/web/
sudo chown -R iamhakim:iamhakim /opt/iamhakim
sudo systemctl restart iamhakim-api
```

## Logs / debug
```bash
journalctl -u iamhakim-api -f
journalctl -u caddy -f
```

## Checks before going public

```bash
curl http://127.0.0.1:5172/api/health
sudo journalctl -u iamhakim-api -n 80 --no-pager
ls /opt/iamhakim/web/en/index.html   # confirms a prerendered page exists
```

Only reload Caddy with the real domain after confirming the API responds locally and the web folder actually contains the prerendered pages.

## RAM (CX23, 4 GB) - cohabitation
- MySQL (shared between bot + site) ~300-400 MB
- .NET API ~150-250 MB
- RootShell bot ~100-200 MB
- Caddy + OS ~400 MB
Comfortable total. Watch it with `htop`. If ever needed: a 2 GB swapfile.
