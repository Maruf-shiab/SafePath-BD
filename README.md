# SafePath BD

**Smart Road Safety & Safe Route Recommendation System**

An ASP.NET Core MVC road-safety platform. The current first-major-milestone implements authentication,
OpenStreetMap/Leaflet mapping, emergency-service discovery, accident/hazard reporting, community review,
moderation, in-app notifications, real operational dashboards, and real OSRM road routing with
verified-incident-aware alternative guidance. Chunk 7 additionally adds synthetic-development traffic ML,
vehicle-specific ETA and intelligent multimodal journey ranking. Authoritative live traffic/transit and the full
road-segment Safety Score Engine remain later production/data work.

Project documentation lives in [docs/](docs) — start with [docs/PROJECT_OVERVIEW.md](docs/PROJECT_OVERVIEW.md).

---


## Current implemented milestone

Implemented in the current codebase:

- Cookie authentication with User / Moderator / Admin authorization.
- Leaflet + OpenStreetMap map, geolocation, search/reverse geocoding and emergency-service discovery.
- Accident and hazard reports with protected evidence images, My Reports and report details.
- Community confirm/dispute voting and comments/replies.
- Moderator/Admin verification workflow with audit history and trusted-accident promotion.
- In-app notifications for verified, rejected, needs-information, duplicate and resolved report outcomes.
- Notification bell, unread count, notification panel, paginated notification center and read controls.
- Real User, Moderator and Admin dashboards backed by database aggregates/projections.
- Backend-mediated OSRM driving routes with provider-neutral routing interfaces.
- Real road geometry, distance, estimated duration, and variable route alternatives.
- Shortest and Fastest route classification (one candidate can carry both labels).
- Public VERIFIED accident/hazard proximity checks with Clear / Caution / Affected states.
- Incident-aware recommended alternatives, bounded road-routed detour fallback, and periodic incident rechecks.
- ML.NET traffic experiment comparing FastTree, LightGBM and SDCA against a baseline, with deterministic split, automatic winner selection, road-holdout evaluation and leakage protection.
- Runtime congestion prediction with documented road/hour fallbacks when the canonical model artifact is unavailable.
- Walk / Rickshaw / Bus / Motorbike / Car ETA using predicted congestion plus development calibration.
- Synthetic-development transit graph with access walking, bus waiting, boarding/transfer time, stop dwell, transfer limits and walking limits.
- Time-dependent Dijkstra, deterministic K-best candidate generation, route diversity and Top-3 explainable journeys.
- Safety Exposure, Route Data Confidence, Route Resilience, backup plan, leave-later analysis and what-if disruption simulation.
- Journey / Traffic / Safety visualization modes and multimodal route timeline.

The supplied traffic and transit data are **synthetic development data**, not measured live traffic or authoritative transit.
A complete populated road-segment Safety Score Engine is still later work; Chunk 7 uses existing mapped scores where genuinely
available and an explicitly labeled verified-incident proxy otherwise. Saved places/routes, continuous GPS, turn-by-turn/voice
navigation and live traffic feeds are not implemented.

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 8.0 (LTS) |
| MySQL Server | 8.x |
| MySQL database | `safepath_bd` (already created) |

---

## Database

> The `safepath_bd` database **already exists** and is the source of truth.

- The schema in [database/SafePath_BD_Full_Database_MySQL.sql](database/SafePath_BD_Full_Database_MySQL.sql) has already been executed.
- **Do not run that script again** — it begins with `DROP DATABASE`.
- This project uses a **database-first** approach. Entity Framework Core maps the existing schema.
- **Never run EF Core migrations**, `Database.Migrate()`, or `Database.EnsureCreated()` against this database.

---

## Configure the connection string

The MySQL password is **never** stored in the repository. It lives in .NET User Secrets.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=safepath_bd;User=root;Password=<YOUR_LOCAL_PASSWORD>;" --project SafePathBD.Web
```

For deployment, supply the same value through the
`ConnectionStrings__DefaultConnection` environment variable instead.

`appsettings.json` intentionally contains an empty placeholder for this key.

---

## Map & geocoding

- **Map rendering:** Leaflet 1.9.4, vendored locally at `wwwroot/lib/leaflet` (no CDN at runtime).
- **Map data:** OpenStreetMap raster tiles from `tile.openstreetmap.org`, tone-mapped in CSS to match the dark UI.
- **Geocoding provider:** OpenStreetMap **Nominatim**, called only from the server through
  `IGeocodingService`. The browser never contacts the provider directly.

Provider settings live under `Geocoding:Nominatim` in `appsettings.json` and contain **no secrets** —
Nominatim only requires a descriptive `UserAgent` identifying the application. Update the contact
detail in that `UserAgent` before any public deployment, and respect the
[Nominatim usage policy](https://operations.osmfoundation.org/policies/nominatim/).

An internet connection is required for map tiles and location search.

---


## Routing

Chunk 6 uses a backend-mediated **OSRM** integration. The browser never calls OSRM directly and cannot choose
the provider URL. Settings live under `Routing:OSRM` in `appsettings.json`. The default public OSRM demo endpoint
is intended for development/academic demonstrations; production should use an appropriate managed or self-hosted
routing service.

Current routing provides:

- real driving road geometry;
- distance and estimated duration;
- Shortest and Fastest labels;
- current public VERIFIED incident checks;
- incident-aware alternative recommendation;
- bounded via-point detour fallback when every normal alternative is seriously affected;
- periodic incident rechecks while a route remains selected.

It does **not** claim live traffic or a full Safest Route. See
[docs/ROUTING_AND_INCIDENT_REROUTING.md](docs/ROUTING_AND_INCIDENT_REROUTING.md).

---

## Intelligent mobility (Chunk 7)

The flagship intelligent planner extends Chunk 6 instead of replacing it. OSRM continues to provide real road corridors. SafePath overlays:

- ML-predicted/estimated congestion from the supplied synthetic AUST-area patterns;
- vehicle-specific ETA for Walk, Rickshaw, Bus, Motorbike and Car;
- time-dependent edge costs at the predicted arrival time of each edge;
- verified incident constraints and available safety coverage;
- walking/transfer constraints and user preference profiles;
- Route Data Confidence, Safety Exposure and Route Resilience;
- Top-3 explainable journeys, a distinct backup when available, departure-window analysis and what-if disruption simulation.

Train the traffic experiment once before using the ML artifact:

```powershell
dotnet run --project SafePathBD.ML.Training
```

The web app **does not train at startup**. If the canonical artifact is missing, it uses the documented synthetic-pattern fallback hierarchy. See [docs/TRAFFIC_ML_METHODOLOGY.md](docs/TRAFFIC_ML_METHODOLOGY.md) and [docs/INTELLIGENT_MULTIMODAL_ROUTING.md](docs/INTELLIGENT_MULTIMODAL_ROUTING.md).

## Optional development data

`emergency_services` ships empty, so the map shows an empty state until facilities exist.
To load six demonstration facilities around Dhaka:

```powershell
mysql -u root -p safepath_bd -e "source database/dev-seed/emergency_services_sample.sql"
```

That script only performs `INSERT`s inside a transaction. It is never executed automatically and is
not part of the schema.

---

## Run

```powershell
dotnet restore
dotnet build
dotnet run --project SafePathBD.Web
```

Use `dotnet watch --project SafePathBD.Web` while editing Razor views, which otherwise need a rebuild.

---

## Solution layout

```text
SafePathBD.sln
├── SafePathBD.Web/          ASP.NET Core MVC application (modular monolith)
├── SafePathBD.ML.Training/  offline ML.NET traffic experiment/training console
└── SafePathBD.Tests/        xUnit test project
```
