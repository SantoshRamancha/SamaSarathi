# Ride App – Tools & Setup Plan

Flutter (mobile) + C# / ASP.NET Core (backend).
Business model: drivers pay a fixed monthly subscription, there's no commission per ride. Each driver sets their own per-km rate, and fares switch to time-based pricing in heavy traffic or rain.

---

## 1. What's already on this PC (checked 2026-10-29)

| Tool | Status | Action |
|---|---|---|
| .NET SDK 10.0.401 (+ 9.x) | ✅ Installed | Use .NET 10 (LTS) |
| Visual Studio 2022 | ✅ Installed | Add "ASP.NET and web development" workload if missing |
| VS Code | ✅ Installed | Add Flutter + C# Dev Kit extensions |
| Git 2.53 | ✅ Installed | — |
| Docker Desktop | ✅ Installed | Runs Postgres/Redis locally |
| Node.js | ✅ Installed | Only needed for the admin panel (optional) |
| Azure CLI | ✅ Installed | For cloud deployment later |
| Android Studio | ✅ Installed | **Android SDK not set up yet**, so run its setup wizard |
| Java | ⚠️ JDK 8 only | Android Gradle needs **JDK 17+** (Android Studio bundles one) |
| Flutter SDK | ❌ Missing | Install |
| Android SDK / adb | ❌ Missing | Install via Android Studio |
| PostgreSQL client | ❌ Missing | Run Postgres in Docker; install pgAdmin or DBeaver |

---

## Setup progress (2026-10-29)

- [x] Flutter 3.47.8 (Dart 3.13.7) cloned to `C:\dev\flutter`, added to user PATH, analytics off
- [x] Flutter uses Android Studio's JDK 17 (`flutter config --jdk-dir`)
- [x] `ANDROID_HOME` fixed. It pointed at another user's profile (`C:\Users\1039\...`) and now points to `%LOCALAPPDATA%\Android\Sdk`
- [x] `dotnet-ef` 10.0.14 installed
- [x] VS Code extensions: Flutter/Dart, C# Dev Kit, Docker
- [ ] **You:** Android Studio first-run wizard to install SDK + emulator
- [ ] **You:** `flutter doctor --android-licenses` (accept the licences)
- [x] Postgres (PostGIS) + Redis via `docker-compose.yml`, initial migration applied
- [x] Backend scaffolded: Domain/Application/Infrastructure/Api + tests (fare rules, 10 tests passing)
- [x] Flutter `core` package + `rider_app` + `driver_app` (Android, iOS, web), verified rider app → API in Chrome
- [x] Phase 1 (2026-10-30): OTP login (dev code 123456), driver registration with document upload, rider profile. 31 backend + 8 Dart tests
- [x] Phase 2 (2026-10-30): admin verification portal (Blazor Server, `admin/`), review API, driver sees approval or requested changes. 39 backend tests
- [x] 2026-11-02: project moved out of OneDrive to **`C:\dev\samasarathi-app`**. OneDrive had turned Flutter's `build/` folders
      into read-only placeholders, so `flutter run` failed with "Flutter failed to delete a directory … build\flutter_assets"
- [x] Phase 3 (2026-11-02): maps (OpenStreetMap via flutter_map), driver Go online/offline with live location in Redis, rider sees nearby drivers. 50 backend tests
- [x] Phase 4 (2026-11-02): rider requests a ride from a chosen driver on the map (fare estimated from the driver's own rate), driver accepts/declines (45 s timeout), either side can cancel, then arrived / start / complete with a final fare, one active ride per rider and per driver enforced in the database. 86 backend tests
- [x] Routing and weather (2026-11-06): quotes use real road routes (OSRM) with a route line on the map, peak-hour windows and live rain (Open-Meteo) to switch to time-based pricing; both fall back gracefully when down. 140 backend tests. **Before launch:** licensed, traffic-aware providers (Google Routes / Ola Maps)
- [x] Destination search (2026-11-02): rider types a place name in Book a ride; backend searches OpenStreetMap Nominatim (cached, rate-limited, swappable via `IPlaceSearch`). 102 backend tests. **Before launch:** replace with Google Places / Ola Maps / a paid provider
- [x] Phase 5a (2026-11-09): driver subscriptions. Free trial starts the first time an approved driver opens the app, grace days after the period ends, drivers without a valid subscription cannot go online or be requested and are taken offline by a background job (rides already under way finish first), driver app shows the plan/history/expiry banner, admin portal has a Subscriptions page with **Add days** (reason required). Price, trial, grace and warning days are config (`Subscription` section), not code. 176 backend tests. **Still to do (5b):** Razorpay payment + webhook (set `Subscription:PaymentsEnabled` when ready)
- [ ] Before launch: real admin phone numbers in `Admin:Phones`, real SMS provider (MSG91/Twilio) for OTP, Azure Blob for documents, production JWT key in a secret store
- `flutter doctor` warns about Visual Studio C++ tools. That's only for Windows desktop apps, so ignore it.

## 2. Install steps (Windows)

```powershell
# Flutter SDK. Install to a path WITHOUT spaces and OUTSIDE OneDrive.
winget install --id Google.Flutter -e      # or download zip and extract to C:\dev\flutter
# then add C:\dev\flutter\bin to PATH

# Android: open Android Studio → More Actions → SDK Manager:
#   SDK Platform (latest), Build-Tools, Platform-Tools, Command-line Tools, Emulator
flutter config --jdk-dir "C:\Program Files\Android\Android Studio\jbr"
flutter doctor --android-licenses
flutter doctor                              # everything should be green except iOS

# Database & cache (local dev)
docker run -d --name ride-pg -e POSTGRES_PASSWORD=devpass -p 5432:5432 postgis/postgis:16-3.4
docker run -d --name ride-redis -p 6379:6379 redis:7

# DB GUI
winget install --id dbeaver.dbeaver -e

# .NET tooling
dotnet tool install --global dotnet-ef
```

VS Code extensions: `Dart-Code.flutter`, `ms-dotnettools.csdevkit`, `ms-azuretools.vscode-docker`.

> **iOS:** building for iPhone needs a Mac with Xcode (or a cloud Mac / Codemagic CI). Start Android-first on Windows.

> **OneDrive warning:** Flutter/.NET `build/`, `bin/`, `obj/` folders generate thousands of files that OneDrive will try to sync, which causes lock errors and slow builds. This happened here, so the code now lives in `C:\dev\samasarathi-app` (not synced) and GitHub is the backup. If you ever see "Flutter failed to delete a directory", check the project isn't under OneDrive, then run `flutter clean`. Also avoid spaces in the project path ("New folder"), because some Android/Gradle tools break on them.

---

## 3. Tech stack

### Mobile (Flutter). Two apps, or one app with two roles
- **Rider app** and **Driver app** (shared `core` package)
- State: `flutter_riverpod` · Routing: `go_router` · HTTP: `dio`
- Maps: `google_maps_flutter` · Location: `geolocator` + background location for drivers
- Realtime: `signalr_netcore` (talks to ASP.NET Core SignalR)
- Push: `firebase_messaging` · Local storage: `flutter_secure_storage`
- Camera/docs upload for driver KYC: `image_picker`, `camera`

### Backend (C# / .NET 10)
- **ASP.NET Core Web API** with Clean Architecture (Api / Application / Domain / Infrastructure)
- **EF Core + Npgsql + NetTopologySuite** on **PostgreSQL + PostGIS** (nearby-driver geo queries)
- **Redis** for live driver locations (GEOADD / GEOSEARCH) and caching
- **SignalR** for live ride tracking, ride requests and driver accept/reject
- **Hangfire** for background jobs (subscription renewals, document expiry reminders)
- Auth: phone OTP → JWT (ASP.NET Core Identity or custom)
- Logging: Serilog · Testing: xUnit + Testcontainers

### Admin / verification portal
- For your team to review driver documents and background checks.
- Built with **Blazor Server** (`admin/Samasarathi.Admin`), so it stays all-C#. It calls the API, the same as the apps;
  it never touches the database directly, so the API remains the single owner of the rules.

---

## 4. Third-party services (accounts to create)

| Need | Suggested service | Notes |
|---|---|---|
| Maps, routing, **live-traffic travel time** | Google Maps Platform (Routes API, `departureTime=now`, `TRAFFIC_AWARE`) or Ola Maps / MapmyIndia | Core to the time-based fare. Its traffic duration tells you when it's "peak" |
| Weather / rain detection | OpenWeatherMap or Tomorrow.io | Triggers rain mode |
| Phone OTP | MSG91, Twilio, or Firebase Auth | |
| Push notifications | Firebase Cloud Messaging | |
| **Driver subscriptions** (monthly fee) | Razorpay Subscriptions or Cashfree | Recurring UPI Autopay / card mandate |
| Rider → driver payment | Direct UPI to driver (no commission) or cash | Keeps the zero-commission promise |
| **Driver verification** | DL check via Parivahan/Sarathi (through a KYC API like IDfy, Signzy, or Surepass), Aadhaar/DigiLocker, police verification / criminal record check (IDfy, AuthBridge) | Background check is your core value |
| Driving behaviour scoring | Phone sensors in driver app (accelerometer: harsh braking, overspeed vs. road limit) + rider ratings | Build in-house over time |
| File storage | Azure Blob Storage (KYC docs, encrypted) | |
| Hosting | Azure App Service / Container Apps + Azure Database for PostgreSQL | Azure CLI already installed |

---

## 5. Fare logic (to agree on before coding)

Driver registers `ratePerKm` (normal traffic). Define a **reference normal speed** per city (e.g. 25 km/h) to derive a time rate:

```
ratePerMinute = ratePerKm × (normalSpeedKmph / 60)

normalFare  = distanceKm × ratePerKm
timeFare    = actualTripMinutes × ratePerMinute

if peak hour OR raining OR (trafficDuration / normalDuration > threshold):
    fare = max(normalFare, timeFare)
else:
    fare = normalFare
```

Show riders an **upfront estimate** using the maps API's traffic duration, then charge the actual time-based fare (with a cap, e.g. +50% over the estimate, so riders trust it).

---

## 6. Proposed repository layout

```
ridesapp/
├─ mobile/
│  ├─ rider_app/        (Flutter)
│  ├─ driver_app/       (Flutter)
│  └─ packages/core/    (shared models, API client, theme)
├─ backend/
│  ├─ src/Rides.Api/
│  ├─ src/Rides.Application/
│  ├─ src/Rides.Domain/
│  ├─ src/Rides.Infrastructure/
│  └─ tests/
├─ admin/               (Blazor or React verification portal)
├─ docker-compose.yml   (postgres + redis)
└─ docs/
```

---

## 7. Next steps
1. Install Flutter + Android SDK, then get `flutter doctor` green.
2. Decide the code location (outside OneDrive recommended) and initialise git.
3. Scaffold backend solution + docker-compose, then the two Flutter apps.
4. Create Google Maps, Firebase and Razorpay test accounts (test keys only for dev).
