# Déploiement iamhakim - VPS Ubuntu 24.04 (Hetzner CX23)

Ton serveur a déjà : **Node 22**, **MySQL 8**, git, build-essential.
À installer : **.NET 10 SDK/runtime** et **Caddy**.

> Important : le projet cible `net10.0`, mais EF Core reste en 9.x parce que `Pomelo.EntityFrameworkCore.MySql 9.x` n’est pas compatible avec EF Core 10.x. Ne remonte pas `Microsoft.EntityFrameworkCore.Design` en 10.x tant que Pomelo 10 stable n’est pas utilisé.
Bot RootShell : inchangé, il continue de tourner à côté.

---

## 0. Utilisateur de service dédié (isolation)

```bash
sudo useradd --system --create-home --shell /usr/sbin/nologin iamhakim
sudo mkdir -p /opt/iamhakim/api /opt/iamhakim/web
sudo chown -R iamhakim:iamhakim /opt/iamhakim
```

## 1. Base MySQL dédiée

RootShell garde sa base. On crée une base + un user séparés pour iamhakim :

```bash
sudo mysql
```
```sql
CREATE DATABASE iamhakim CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'iamhakim'@'localhost' IDENTIFIED BY 'UN_MOT_DE_PASSE_FORT';
GRANT ALL PRIVILEGES ON iamhakim.* TO 'iamhakim'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```
Reporte ce mot de passe dans `iamhakim-api.service` (variable `ConnectionStrings__Default`).

## 2. Installer .NET 10

```bash
# Microsoft feed (Ubuntu 24.04)
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/ms.deb
sudo dpkg -i /tmp/ms.deb
sudo apt update
sudo apt install -y dotnet-sdk-10.0
dotnet --version   # vérifie
```

## 3. Récupérer le code et builder

```bash
cd /tmp && git clone <ton-repo> iamhakim   # ou scp ton zip
cd iamhakim

# --- API ---
cd backend/IAmHakim.Api
dotnet publish -c Release -o /opt/iamhakim/api
# applique les migrations (crée les tables dans la base iamhakim)
# (export temporaire de la connexion pour la commande EF)
export ConnectionStrings__Default="server=localhost;port=3306;database=iamhakim;user=iamhakim;password=UN_MOT_DE_PASSE_FORT;TreatTinyAsBoolean=false"
export Security__ClientIdentity__IpHashSalt="GENERE_UN_LONG_SECRET_ALEATOIRE"
# Active Turnstile only after creating a widget in Cloudflare.
# export Security__Turnstile__Enabled=true
# export Security__Turnstile__SiteKey="0x4AAAAA_PUBLIC_SITE_KEY"
# export Security__Turnstile__SecretKey="0x4AAAAA_PRIVATE_SECRET_KEY"
dotnet tool install --global dotnet-ef --version 9.*   # si pas déjà fait
~/.dotnet/tools/dotnet-ef database update --project .
#  (sinon l'app applique les migrations elle-même au démarrage ; ne masque pas une vraie erreur de migration)

# --- WEB (Angular SSR) ---
cd ../../frontend
npm ci
npm run build
sudo mkdir -p /opt/iamhakim/web
sudo rm -rf /opt/iamhakim/web/dist
sudo cp -r dist /opt/iamhakim/web/
sudo chown -R iamhakim:iamhakim /opt/iamhakim
```

> Note : l'app applique automatiquement les migrations EF au démarrage
> (`MigrateAsync`), donc l'étape `database update` est optionnelle.

## 4. systemd - API + Web

Les deux services sont en `Type=simple`. L’API écoute seulement sur `127.0.0.1:5172`, donc elle n’est pas exposée directement à Internet.

```bash
sudo cp deploy/iamhakim-api.service /etc/systemd/system/
sudo cp deploy/iamhakim-web.service /etc/systemd/system/
# ÉDITE iamhakim-api.service : mets le vrai mot de passe MySQL
sudo nano /etc/systemd/system/iamhakim-api.service

sudo systemctl daemon-reload
sudo systemctl enable --now iamhakim-api iamhakim-web
sudo systemctl status iamhakim-api iamhakim-web   # doivent être "active (running)"
```

## 5. Caddy - reverse proxy + HTTPS auto

```bash
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update && sudo apt install -y caddy

sudo cp deploy/Caddyfile /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Avant de reload : **pointe le DNS** de `iamhakim.com` (et `www`) vers `REDACTED_VPS_IP`
(enregistrements A). Caddy obtiendra le certificat tout seul au premier accès.

## 6. Firewall

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```
Les ports 5172 (API) et 4000 (SSR) restent **internes** (bind 127.0.0.1) - pas ouverts.

---

## Mettre à jour plus tard

```bash
cd /tmp/iamhakim && git pull
cd backend/IAmHakim.Api && dotnet publish -c Release -o /opt/iamhakim/api
cd ../../frontend && npm ci && npm run build && sudo rm -rf /opt/iamhakim/web/dist && sudo cp -r dist /opt/iamhakim/web/
sudo chown -R iamhakim:iamhakim /opt/iamhakim
sudo systemctl restart iamhakim-api iamhakim-web
```

## Logs / debug
```bash
journalctl -u iamhakim-api -f
journalctl -u iamhakim-web -f
journalctl -u caddy -f
```

## Vérifications avant ouverture publique

```bash
curl -I http://127.0.0.1:4000
curl http://127.0.0.1:5172/api/health
sudo journalctl -u iamhakim-api -n 80 --no-pager
sudo journalctl -u iamhakim-web -n 80 --no-pager
```

Ne reload Caddy avec le domaine qu’après avoir confirmé que ces deux services répondent en local.

## RAM (CX23, 4 Go) - cohabitation
- MySQL (partagé bot + site) ~300-400 Mo
- API .NET ~150-250 Mo
- SSR Node ~100-200 Mo
- bot RootShell ~100-200 Mo
- Caddy + OS ~400 Mo
Total confortable. Surveille avec `htop`. Si besoin un jour : swapfile de 2 Go.
