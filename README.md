# SafePath BD

**Smart Road Safety & Safe Route Recommendation System**

An ASP.NET Core MVC application that combines map navigation, road-segment safety scoring,
community accident/hazard reporting, and emergency-service discovery.

Project documentation lives in [docs/](docs) — start with [docs/PROJECT_OVERVIEW.md](docs/PROJECT_OVERVIEW.md).

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
├── SafePathBD.Web/      ASP.NET Core MVC application (modular monolith)
└── SafePathBD.Tests/    xUnit test project
```
