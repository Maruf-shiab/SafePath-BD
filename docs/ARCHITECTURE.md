# SafePath BD — Architecture

## 1. Architecture Goal

SafePath BD should use a **simple modular monolith** built with ASP.NET Core MVC.

The project must remain understandable for a university team and easy for GitHub Copilot/Opus to maintain. Do **not** introduce a large multi-project Clean Architecture solution unless explicitly requested later.

The architecture should keep responsibilities separated inside one ASP.NET Core web project.

> **Important:** The MySQL database already exists. The application is being built around that schema.

---

## 2. Recommended High-Level Structure

The coding assistant should aim for a structure similar to:

```text
SafePathBD/
│
├── docs/
│   ├── PROJECT_OVERVIEW.md
│   ├── ARCHITECTURE.md
│   ├── DATABASE_SCHEMA.md
│   ├── FEATURES.md
│   ├── API_GUIDELINES.md
│   ├── CODING_GUIDELINES.md
│   └── DEVELOPMENT_PLAN.md
│
├── database/
│   └── SafePath_BD_Full_Database_MySQL.sql
│
├── SafePathBD.Web/
│   ├── Areas/
│   │   └── Admin/
│   ├── Controllers/
│   ├── Data/
│   ├── Models/
│   │   ├── Entities/
│   │   ├── DTOs/
│   │   └── ViewModels/
│   ├── Services/
│   │   ├── Interfaces/
│   │   └── Implementations/
│   ├── Integrations/
│   ├── Security/
│   ├── Common/
│   ├── Views/
│   ├── wwwroot/
│   ├── Program.cs
│   └── appsettings.json
│
└── SafePathBD.Tests/
```

This is a target structure, not a requirement to create every folder immediately. Create folders only when a feature needs them.

---

## 3. Architectural Layers Inside the Web Project

### 3.1 Presentation Layer

Contains:

- MVC Controllers
- Razor Views
- ViewModels
- Client-side JavaScript
- CSS
- Admin Area

Responsibilities:
- Accept requests.
- Validate basic request shape.
- Call application services.
- Prepare ViewModels/DTOs.
- Return HTML or JSON.
- Never contain complex business calculations.

Example:

```text
RouteController
    ↓
IRouteService
    ↓
RouteService
```

The controller should not calculate safety scores itself.

---

### 3.2 Service / Business Layer

Located under `Services/`.

This is the most important layer.

Services should implement business rules such as:

- Authentication and authorization support.
- Accident report creation.
- Hazard report creation.
- Report verification.
- Safety-score calculation.
- Route comparison.
- Emergency-service lookup.
- Notification creation.
- Saved route/place operations.
- Admin operations.

Suggested interfaces:

```text
IAuthService
IUserService
IMapService
IRouteService
ISafetyScoreService
IReportService
IAccidentService
IHazardService
IEmergencyService
INotificationService
IAdminService
```

Do not create all interfaces on day one. Add them as modules are implemented.

---

### 3.3 Data Access Layer

Located under `Data/`.

Contains:

- `SafePathDbContext`
- EF Core entity mappings/configuration
- Optional query helpers
- Database seeding helper only if required for application-level data

Because the database is already created, the initial approach is **database-first**.

Preferred flow:

```text
Existing MySQL Schema
        ↓
EF Core Model/Scaffolding
        ↓
SafePathDbContext
        ↓
Services
        ↓
Controllers
```

The application must not use EF migrations to silently redesign the existing schema.

---

### 3.4 Integration Layer

Located under `Integrations/`.

All third-party services belong here.

Suggested areas:

```text
Integrations/
├── Maps/
├── Routing/
├── Geocoding/
├── Weather/
└── Notifications/
```

Provider-specific code should implement application-facing interfaces.

Example:

```text
IRoutingProvider
        ↑
OsrmRoutingProvider
```

This allows the routing provider to be replaced later without rewriting `RouteService`.

---

## 4. Dependency Rule

The simple rule is:

```text
Controller / Razor
      ↓
Service
      ↓
DbContext and/or Integration Interface
      ↓
MySQL / External API
```

Avoid:

```text
View → DbContext
Controller → raw SQL business logic
Controller → external API directly
JavaScript → database
```

---

## 5. Authentication Architecture

The database already has:

- `users`
- `roles`
- `user_roles`

Therefore, do not automatically replace the schema with ASP.NET Identity tables.

Recommended Version 1 approach:

1. Register user.
2. Hash password securely.
3. Store hash in `users.password_hash`.
4. On login, verify password.
5. Load roles through `user_roles`.
6. Issue ASP.NET Core authentication cookie.
7. Add role claims such as `Admin`, `Moderator`, `User`.
8. Use `[Authorize]` and `[Authorize(Roles = "...")]`.

Never store plain-text passwords.

Never expose `password_hash` in a ViewModel or API response.

---

## 6. Map Architecture

### Frontend

Leaflet can render:

- Base map.
- Start/destination markers.
- Accident markers.
- Hazard markers.
- Emergency services.
- Route polylines.
- Risk-colored segments if desired.

### Backend

The server should provide JSON endpoints for:

- Location search.
- Public report markers.
- Emergency services.
- Candidate route information.
- Safety score details.

The browser should not contain database credentials or private API secrets.

---

## 7. Route Recommendation Architecture

Route generation and safety evaluation are separate concerns.

### Stage 1 — Candidate Route Generation

Input:
- Start latitude/longitude.
- Destination latitude/longitude.

External routing provider may return:
- Route geometry/polyline.
- Distance.
- Duration.
- Route alternatives.

### Stage 2 — Match Route to Road Segments

Where possible, determine which `road_segments` are covered by each candidate route.

For a university project, exact production-grade map matching is not mandatory. A practical approach can be implemented and documented.

### Stage 3 — Segment Safety

Use the latest safety score from:

- `safety_scores`
- `vw_latest_segment_safety_score`

If no score exists, calculate one or use a documented neutral/default fallback.

### Stage 4 — Route-Level Safety

A route-level score should be based on its segments.

A reasonable Version 1 formula is a distance-weighted average:

```text
RouteSafety =
Σ(SegmentSafetyScore × SegmentDistance)
---------------------------------------
Σ(SegmentDistance)
```

Optional penalties can be added for critical segments.

### Stage 5 — Route Comparison

- **Shortest:** minimum distance.
- **Fastest:** minimum estimated duration.
- **Safest:** highest route safety score, subject to reasonable route validity.

The same candidate route may be both fastest and shortest; do not force artificial differences.

---

## 8. Safety Score Architecture

`ISafetyScoreService` should calculate an explainable 0–100 score.

Data sources:

- `accidents`
- verified/active `reports` + `hazard_reports`
- `road_conditions`
- `traffic_conditions`
- `weather_conditions`
- lighting information from `road_conditions`

Configuration comes from `system_settings`.

Initial weights:

```text
Accident       0.35
Hazard         0.25
Road condition 0.15
Traffic        0.10
Weather        0.10
Lighting       0.05
```

Concept:

```text
weightedRisk =
 accidentRisk × accidentWeight
+ hazardRisk × hazardWeight
+ roadConditionRisk × roadConditionWeight
+ trafficRisk × trafficWeight
+ weatherRisk × weatherWeight
+ lightingRisk × lightingWeight

safetyScore = 100 - weightedRisk
```

All normalized risk factors should be constrained to 0–100.

Store:

- final result in `safety_scores`
- each factor contribution in `safety_score_factors`

This is important because the route recommendation should be explainable.

---

## 9. Reporting Architecture

A report uses inheritance-like relational design.

### Parent

`reports`

Contains shared data:
- report type
- reporter
- location
- road segment
- status
- title
- description
- timestamps

### Subtype

For an accident:

```text
reports
   1
   │
   1
accident_reports
```

For a hazard:

```text
reports
   1
   │
   1
hazard_reports
```

The database triggers enforce correct subtype behavior.

Shared report attachments/community data reference `reports`:

- `report_images`
- `report_votes`
- `report_comments`
- `report_verifications`

This design must be preserved.

---

## 10. Verified Accident Architecture

`accident_reports` and `accidents` have different meanings.

### `accident_reports`
Community-submitted claims.

### `accidents`
Verified/historical accident facts used by the safety engine.

A verified accident report may become the source for an `accidents` record using `source_report_id`.

Do not calculate long-term accident risk directly from every unverified community report.

---

## 11. Emergency Service Architecture

Emergency services are stored in:

- `emergency_service_types`
- `emergency_services`
- `locations`

The application can calculate nearest facilities using latitude/longitude.

For a simple Version 1 implementation, Haversine distance is acceptable for nearby ranking.

Road-network travel time can be added later.

---

## 12. Notification Architecture

Database notifications are stored in `notifications`.

Notifications can reference:

- a report
- a route
- a road segment

Initial notification delivery is in-app.

Email/push notification can be future integrations.

Possible events:

- Report verified.
- Report rejected.
- Report resolved.
- Hazard on route.
- Accident on route.
- Road risk increased.
- System message.

---

## 13. Admin Architecture

Use an MVC Area:

```text
Areas/Admin/
```

Recommended admin modules:

- Dashboard
- Users
- Reports
- Roads
- Road Segments
- Accidents
- Emergency Services
- Safety Scores
- Feedback
- Settings
- Audit Log

Authorization:

```csharp
[Authorize(Roles = "Admin,Moderator")]
```

Use stricter Admin-only authorization for configuration/user-role operations.

---

## 14. Error Handling

Use centralized exception handling.

Expected behavior:

- Validation errors → friendly user message / HTTP 400 for API.
- Authentication missing → login challenge / HTTP 401.
- Permission denied → HTTP 403.
- Entity missing → HTTP 404.
- Conflict → HTTP 409.
- Unexpected exception → logged server-side, generic HTTP 500 response.

Never show stack traces or database credentials to end users.

---

## 15. Configuration and Secrets

Safe values may stay in `appsettings.json`.

Secrets must not be committed:
- MySQL password.
- API keys.
- SMTP secrets.
- Private tokens.

Use:
- User Secrets for local development.
- Environment variables for deployment.

---

## 16. Performance Principles

Use indexes already defined by the database.

Important query rules:
- Use `AsNoTracking()` for read-only queries.
- Project only required columns.
- Paginate long lists.
- Do not load all map reports for the entire country when the visible map bounds can be used.
- Prefer latest-score view where appropriate.
- Cache expensive external routing/weather calls only after correctness is established.
- Avoid N+1 queries.

---

## 17. Architecture Rule for AI Assistants

Before implementing a module:

1. Read all seven docs.
2. Check actual database table/column names.
3. Reuse existing entities/services.
4. Do not invent schema.
5. Implement one module at a time.
6. Build the project.
7. Fix all compilation errors.
8. Verify the current phase before starting another phase.
