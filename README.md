# Samasarathi

Commission-free ride app. Drivers pay a fixed monthly subscription and set their own per-km rate.
In peak hours, rain or heavy traffic, fares are charged by travel time instead of distance.

See [SETUP.md](SETUP.md) for tools, services and the setup checklist, and [TESTING.md](TESTING.md)
for a step-by-step manual test of every flow.

## Layout

```
backend/                     C# / ASP.NET Core (.NET 10)
  src/Samasarathi.Domain         Business rules: entities, FareCalculator. No dependencies.
  src/Samasarathi.Application    Interfaces (e.g. IDriverLocationStore) + API contracts shared with the admin portal
  src/Samasarathi.Infrastructure EF Core/PostgreSQL, Redis. Implements those interfaces.
  src/Samasarathi.Api            HTTP endpoints, startup, config
  tests/Samasarathi.Tests        xUnit tests
mobile/
  packages/core              Shared Dart: API client, models, theme
  rider_app                  Rider Flutter app
  driver_app                 Driver Flutter app
admin/
  Samasarathi.Admin          Verification portal for our team (Blazor Server, calls the API)
docker-compose.yml           Local PostgreSQL (PostGIS) + Redis
```

Dependencies point inward: `Api → Infrastructure → Application → Domain`.
The fare rules in Domain can be tested without a database or web server.

## Run locally

0. **First time only: create your local settings file.** The API needs a database password and a login-token signing key, and these
   are never committed to git. Copy the example file and fill in the two placeholders:
   ```bash
   cp backend/src/Samasarathi.Api/appsettings.Development.example.json backend/src/Samasarathi.Api/appsettings.Development.json
   ```
   (PowerShell: `Copy-Item backend/src/Samasarathi.Api/appsettings.Development.example.json backend/src/Samasarathi.Api/appsettings.Development.json`)
   - `Password=...` must match the database password. If you didn't set `POSTGRES_PASSWORD` before `docker compose up`, the
     local container's password is `devpass`.
   - `Jwt:Key`: any random string of 32+ characters. Generate one with PowerShell:
     `[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])`
   - `appsettings.Development.json` is git-ignored, so your values stay on your machine. In production, set these through the host's
     secret store or environment variables (`Jwt__Key`, `ConnectionStrings__Postgres`), never in a file in the repo.

1. Start the database and Redis (Docker Desktop must be running):
   ```bash
   docker compose up -d
   ```
2. Apply database migrations (first time, and after each new migration):
   ```bash
   dotnet ef database update -p backend/src/Samasarathi.Infrastructure -s backend/src/Samasarathi.Api
   ```
3. Start the API at http://localhost:5299:
   ```bash
   dotnet run --project backend/src/Samasarathi.Api --launch-profile http
   ```
   Try requests from `backend/src/Samasarathi.Api/Samasarathi.Api.http` (VS Code / Visual Studio "Send Request").
4. Run an app (from `mobile/rider_app` or `mobile/driver_app`):
   ```bash
   flutter run -d chrome
   ```
   On the Android emulator, the PC's localhost is `10.0.2.2`:
   ```bash
   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5299
   ```
   **If `flutter run -d chrome` crashes** with `FileSystemException: writeFrom failed ... The pipe is being closed` and
   `The Dart compiler exited unexpectedly`: that is a Flutter tooling problem (the Chrome debugger asks the compiler to evaluate an
   expression after a hot restart or after the tool is closing), not an app bug. Run the app without the Chrome debugger instead, on a
   fixed port, and open the address in your normal Chrome:
   ```bash
   flutter run -d web-server --web-port 5301
   ```
   Use 5301 for the rider app and 5302 for the driver app, then open http://localhost:5301 or http://localhost:5302. Hot restart (`R`)
   still works. A fixed port also keeps you logged in between runs (a random port is a new site every time, so the saved login is lost).
   Always stop with `q` in the terminal, never by closing the Chrome tab. If it still misbehaves: `flutter clean`, then `flutter pub get`.

5. Start the admin portal at http://localhost:5310 (needs the API running):
   ```bash
   dotnet run --project admin/Samasarathi.Admin --launch-profile http
   ```

## Logging in during development

- Use any valid Indian mobile number (10 digits, starting 6–9).
- The OTP is always **123456** (`Otp:DevCode` in `appsettings.Development.json`). No SMS is sent; the code is printed in the API console as `DEV SMS to ...`.
- A new code can be requested only every 30 seconds, and 5 wrong tries throw the code away.
- The same number can be both a rider and a driver; each app logs in separately.
- **Admin portal:** log in with **9999999999**. Only numbers listed under `Admin:Phones` in
  `appsettings.Development.json` (API project) get admin access; add your own number there.
  Admin logins last 12 hours, and closing the browser tab logs you out.

## What works so far

| App | Flow |
|---|---|
| Driver | OTP login → details → vehicle → upload photo, licence, RC, ID → set rate (with sample earnings) → submit → "Under verification" status with 5 pending checks |
| Admin | OTP login → review queue (longest waiting first) → open a driver → view each document (images and PDFs) → pass/fail each check with a note → approve, or send back with a message |
| Driver (after review) | Approved: status shows "Approved". Sent back: sees the team's message, fixes details, submits again (all checks restart; earlier results stay in the history) |
| Driver (approved) | Map → **Go online** (shares position every 5 s) → **Go offline**. Logging out also goes offline |
| Rider | OTP login → name (first time only) → home map showing nearby online drivers → **book a ride**: search for or tap the destination, choose a driver, see the fare, request → wait for the driver to accept (or decline / time out) → see driver details; can cancel |
| Driver (approved, online) | Gets a **ride request** card (rider, pickup distance, fare, 45 s countdown) → **Accept** or **Decline**; after accepting: **I've arrived → Start trip → Complete trip** (shows the fare to collect), can cancel before the trip starts |

Review rules (in `Driver.cs`, covered by tests): a failed check needs a note, a rejection needs a reason,
and a driver can be approved only when all 5 checks have passed.

Uploaded files are saved in `backend/src/Samasarathi.Api/uploads/` (not committed to git).

### API endpoints

| Method | Path | Who |
|---|---|---|
| POST | `/api/auth/otp/request` | anyone |
| POST | `/api/auth/otp/verify` | anyone → returns a login token |
| GET | `/api/drivers/me` | driver |
| PUT | `/api/drivers/me/profile`, `/vehicle`, `/rate` | driver |
| POST | `/api/drivers/me/documents` (multipart: `type`, `file`) | driver |
| POST | `/api/drivers/me/submit` | driver |
| GET / PUT | `/api/riders/me` | rider |
| POST | `/api/drivers/me/online`, `/offline`; PUT `/api/drivers/me/location` (`latitude`, `longitude`) | driver (approved) |
| GET | `/api/riders/me/nearby-drivers?latitude=&longitude=` | rider |
| GET | `/api/places/search?query=&latitude=&longitude=` (destination search) | rider |
| GET | `/api/rides/quote?driverId=&pickupLatitude=&pickupLongitude=&dropLatitude=&dropLongitude=` | rider |
| POST | `/api/rides` (same fields) | rider |
| GET | `/api/rides/active`, `/api/rides/{id}`; POST `/api/rides/{id}/cancel` | rider |
| GET | `/api/drivers/me/ride` (204 when none); POST `/api/drivers/me/ride/{id}/accept`, `/decline`, `/arrived`, `/start`, `/complete`, `/cancel` | driver |
| POST | `/api/fares/estimate` | anyone |
| GET | `/api/admin/drivers/counts` | admin |
| GET | `/api/admin/drivers?status=InReview&search=` | admin |
| GET | `/api/admin/drivers/{id}` | admin |
| GET | `/api/admin/drivers/{id}/documents/{type}` (the file itself) | admin |
| PUT | `/api/admin/drivers/{id}/checks/{type}` (`result`, `notes`) | admin |
| POST | `/api/admin/drivers/{id}/approve`, `/reject` (`reason`) | admin |

Interactive API docs: http://localhost:5299/scalar (Development only).

## Tests

```bash
dotnet test backend
```
```bash
flutter test mobile/packages/core
```
The two apps have their own small test folders (`cd mobile/driver_app && flutter test`, same for `rider_app`).

GitHub Actions (`.github/workflows/ci.yml`) runs all of this on every push to `main` and every pull request: it builds the API and
admin portal, runs the backend tests, and runs `flutter analyze` + `flutter test` for the core package and both apps.

## Routing, weather and fares

Fare quotes use a real road route (OSRM) and the current rain at the pickup (Open-Meteo), both free and key-less for development.
Peak hours come from time windows in config, because neither service knows live traffic:

| Config section | What it does |
|---|---|
| `Routing` | OSRM server URL and cache time |
| `Weather` | Open-Meteo URL, rain threshold; `ForceRain: true` is a testing switch that reports rain everywhere |
| `PeakHours` | `Windows` (local `HH:mm-HH:mm`), `TimeZone`, `WeekdaysOnly` |
| `Traffic` | `PeakSlowdown` (1.5) and `RainSlowdown` (1.3): how much slower than free-flow a trip is expected to be |

Peak hour or rain switches the fare to *time-based* pricing (see `FareCalculator`). If routing or weather is down, the quote still
works (straight-line distance, dry weather) and says so. To change a provider, write another `IRouteService` / `IWeatherService` and
change one line in `DependencyInjection.cs`. **Before launch:** OSRM's demo server and Open-Meteo's free tier are not for commercial
use; switch to Google Routes (traffic-aware) / Ola Maps and a licensed weather provider.

### Support contact

The "Contact support" buttons in both apps (call, WhatsApp, email) read the `Support` section of `appsettings.json` (`Phone`, `WhatsApp`, `Email`, `Hours`), or environment variables such as `Support__WhatsApp`. Leave a value empty to hide it. These are public contact details, not secrets. Restart the API to change them; no app update is needed.

## Driver subscriptions

Drivers pay a fixed fee instead of a commission. Every business number is **configuration**, so it can change when the business
decides, with no code change and no app update (the apps read the numbers from the API). Edit the `Subscription` section of
`appsettings.json`, or set an environment variable such as `Subscription__MonthlyPrice=599`, then restart the API.

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. `false` = nobody needs a subscription |
| `PlanName`, `MonthlyPrice` | Monthly, ₹499 | The one plan shown to drivers |
| `PlanDays` | 30 | Length of one paid period |
| `TrialDays` | 30 | Free days when an approved driver first opens the app (`0` = no trial) |
| `GraceDays` | 3 | Days the driver can keep working after the period ends (`0` = cut off at once) |
| `ExpiryWarningDays` | 3 | Days before the end when the apps start warning |
| `PaymentsEnabled` | `false` | Flip to `true` once Razorpay payment exists; until then drivers are told to contact support |
| `ExpiryCheckMinutes` | 10 | How often the background job takes lapsed drivers offline |

A driver without a valid subscription cannot go online, is hidden from riders, and cannot be requested. A ride already in progress
always finishes. Our team can add days (with a reason) from the admin portal's **Subscriptions** page. Pricing per vehicle type or
several plans would mean a small `Plan` table instead of these settings: not needed yet.

## Maps and location

Destination search goes through our API (`/api/places/search`) to OpenStreetMap's free Nominatim service; the API sets the required
`User-Agent` (config `PlaceSearch:UserAgent`: put a real contact email there), spaces calls one second apart and caches results
for 10 minutes. Switching provider means writing another `IPlaceSearch` and changing one line in `DependencyInjection.cs`.

Both apps use one `AppMap` widget (`mobile/packages/core/lib/src/map/app_map.dart`), which today draws free OpenStreetMap
tiles. Those are for development only; before launch switch to a paid tile provider or Google Maps by changing just that file.
In debug builds, tapping the map moves "you", handy on a PC where GPS never moves. Live driver positions live in Redis;
a driver silent for 60 s drops off riders' maps, and riders see drivers within 3 km (`Location` in `appsettings.json`).

## Common tasks

| Task | Command |
|---|---|
| Add a DB migration after changing entities | `dotnet ef migrations add <Name> -p backend/src/Samasarathi.Infrastructure -s backend/src/Samasarathi.Api -o Persistence/Migrations` |
| Add a Flutter package | `flutter pub add <package>` (inside the app folder) |
| Faster Flutter web build (skips the extra WebAssembly test compile) | `flutter build web --no-wasm-dry-run` |
| Stop DB/Redis | `docker compose down` (data is kept in the `pgdata` volume) |
