# iamhakim - setup & ops

## Base de données (EF Core + MySQL)

- La base locale/prod est **MySQL**.
- Le schéma est géré par **migrations EF Core** (`backend/IAmHakim.Api/Migrations/`).
- Au démarrage, l'app applique automatiquement les migrations en attente (`MigrateAsync`).
- En production, la connexion est passée via `ConnectionStrings__Default` dans `deploy/iamhakim-api.service`.

### Première fois en local

Crée une base locale dédiée :

```sql
CREATE DATABASE iamhakim CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'iamhakim'@'localhost' IDENTIFIED BY 'dev_change_me';
GRANT ALL PRIVILEGES ON iamhakim.* TO 'iamhakim'@'localhost';
FLUSH PRIVILEGES;
```

Puis lance :

```bash
dotnet run --project backend/IAmHakim.Api
```

La migration `InitialCreate` crée toutes les tables (`SiteStats`, `SiteEvents`, `Bookings`).

### À chaque changement de modèle ensuite

```bash
dotnet ef migrations add NomDuChangement --project backend/IAmHakim.Api
```

La base se met à jour seule au prochain démarrage - sans perte de données.
Si `dotnet ef` manque :

```bash
dotnet tool install --global dotnet-ef --version 9.*
```

## Lancer en dev

1. Backend : `dotnet run --project backend/IAmHakim.Api` (écoute sur http://localhost:5172)
2. Frontend : `cd frontend && npm install && npm start`

Le proxy (`frontend/proxy.conf.json`) renvoie `/api` et `/hubs` vers le backend.

## Langues (i18n)

- Détection automatique de la langue du navigateur (EN / FR / NL), sélecteur dans le header.
- Dictionnaires : `frontend/src/app/i18n/translations.ts`.
- Les pages principales utilisent maintenant des clés i18n au lieu de chaînes hardcodées.

## Booking request workflow

The public booking form now creates a **pending request**, not a directly confirmed appointment.

Flow:

```text
visitor selects a slot
-> API validates availability + anti-spam rules
-> Booking row is stored with Status = pending
-> visitor receives a "request received" e-mail
-> Hakim receives an admin decision e-mail
-> Hakim opens the private decision page
-> Accept creates the calendar event and sends final confirmation
-> Reject frees the slot and sends a polite refusal
```

Important details:

- `pending` and `accepted` requests block the slot in public availability.
- Pending requests expire after `Booking:PendingExpirationHours`.
- Calendar events are created only after acceptance.
- Admin accept/reject links open a page first; the real action is done by POST buttons to avoid mail-scanner auto-click issues.

### Required production secrets

Use environment variables or your server secret store, not Git:

```bash
Mail__Enabled=true
Mail__FromEmail=booking@iamhakim.com
Mail__FromName=Hakim
Mail__AdminEmail=your.personal.inbox@example.com
Mail__ReplyToEmail=contact@iamhakim.com
Mail__ResendApiKey=re_...
```

For anti-bot protection in production:

```bash
Security__ClientIdentity__IpHashSalt=generate-a-long-random-secret
Security__Turnstile__Enabled=true
Security__Turnstile__SiteKey=0x4AAAAA_public_site_key
Security__Turnstile__SecretKey=0x4AAAAA_private_secret_key
```

For Google Calendar live mode:

```bash
Booking__Mode=live
Booking__Google__Enabled=true
Booking__Google__IsPrimary=true
Booking__Google__CalendarId=primary
Booking__Google__OwnerEmail=your@gmail.com
Booking__Google__ClientId=...
Booking__Google__ClientSecret=...
Booking__Google__RefreshToken=...
```

Anti-spam defaults combine e-mail limits and salted-IP limits. Raw IP addresses are not stored; the API stores only the salted hash used for rate limits and live-counter deduplication:

```json
"AntiSpam": {
  "MaxPendingPerEmail": 2,
  "CooldownMinutesPerEmail": 60,
  "MaxRequestsPerEmailPerDay": 5,
  "MaxPendingPerIp": 3,
  "CooldownMinutesPerIp": 10,
  "MaxRequestsPerIpPerDay": 12,
  "MaxVerificationCodesPerIpPerHour": 8,
  "MaxVerificationCodesPerIpPerDay": 20
}
```
