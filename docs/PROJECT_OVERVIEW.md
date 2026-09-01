# SafePath BD — Project Overview

## 1. Project Identity

**Project Name:** SafePath BD  
**Full Title:** SafePath BD — Smart Road Safety & Safe Route Recommendation System  
**Project Type:** ASP.NET Core web application  
**Primary Database:** MySQL (`safepath_bd`)  
**Primary Purpose:** Help people travel more safely by combining map-based navigation, road-risk information, community reporting, emergency assistance, and route safety scoring in one platform.

> **Instruction for any AI coding assistant:** Read this file together with `ARCHITECTURE.md`, `DATABASE_SCHEMA.md`, `FEATURES.md`, `API_GUIDELINES.md`, `CODING_GUIDELINES.md`, and `DEVELOPMENT_PLAN.md` before generating or modifying project code. The MySQL database already exists and is the source of truth.

---

## 2. Core Project Idea

Most navigation platforms mainly optimize a journey for **distance or travel time**. SafePath BD adds another important dimension: **road safety**.

A user will be able to choose a start point and destination on a map. The system may obtain candidate routes from a routing provider, divide or match those routes against known road segments, evaluate the risk of those segments, and present route choices such as:

- **Safest Route**
- **Fastest Route**
- **Shortest Route**

The safest route is not necessarily the shortest. A slightly longer route may be recommended when it passes through road segments with fewer verified accidents, fewer active hazards, better road conditions, safer traffic conditions, better lighting, or lower weather-related risk.

SafePath BD also works as a community road-safety platform. Users can report accidents and hazards with map locations and supporting details. Other users may confirm or dispute reports. Administrators or moderators can review and verify reports before they are treated as trusted information.

---

## 3. Real-World Problem

Road users often lack one place where they can:

1. Compare routes using road-safety information.
2. See accident-prone or hazardous locations on a map.
3. Report potholes, broken signals, waterlogging, roadblocks, dangerous intersections, accidents, and similar problems.
4. Know whether a community-submitted report has been verified.
5. Find nearby emergency services from the same platform.
6. Understand why a road or route is considered safe or risky.

Existing navigation tools may provide traffic and incident information, but this project is focused specifically on **structured road-safety scoring, verified community reporting, road-segment risk analysis, and safety-aware route recommendation**.

---

## 4. Main Objectives

SafePath BD should:

- Provide an interactive road-safety map.
- Recommend a safer route in addition to fastest and shortest alternatives.
- Calculate and store a **safety score for individual road segments**.
- Use verified accidents, hazards, road condition, traffic, weather, and lighting-related information as safety factors.
- Allow users to report accidents and road hazards.
- Support report images, comments, confirmations/disputes, and verification history.
- Keep user-submitted reports separate from verified historical accident records.
- Display accident/hazard hotspots.
- Help users locate nearby hospitals, police stations, fire services, ambulances, and emergency centers.
- Notify users about relevant road-safety events.
- Provide an administrative dashboard for report verification, road information, emergency services, settings, and analytics.
- Keep the design practical enough for an academic ASP.NET project while allowing future expansion.

---

## 5. Sustainable Development Goal Alignment

The project primarily aligns with:

### SDG 3 — Good Health and Well-Being
SafePath BD supports road-traffic injury reduction by helping users become aware of dangerous road conditions and by encouraging safer route selection.

### SDG 11 — Sustainable Cities and Communities
The platform also supports safer and more accessible urban transportation by collecting, organizing, and presenting information about road hazards, infrastructure condition, and emergency facilities.

---

## 6. Main Actors

### 6.1 Guest
A non-authenticated visitor.

Typical permissions:
- View public landing page.
- View selected public map information.
- Search or preview routes if allowed by implementation.
- View public accident/hazard markers.
- Find emergency services.
- Register or log in.

### 6.2 Registered User
A normal authenticated user.

Typical permissions:
- Use all public functionality.
- Request safest/fastest/shortest routes.
- Save places and routes.
- Submit accident reports.
- Submit hazard reports.
- Upload report images.
- Comment on reports.
- Confirm or dispute reports.
- Receive notifications.
- Manage profile and saved data.
- Submit feedback.

### 6.3 Moderator
A trusted user with review responsibilities.

Typical permissions:
- Review pending reports.
- Add verification decisions/comments.
- Mark reports verified, rejected, duplicate, needs more information, or resolved.
- Help maintain trustworthy road-safety information.

### 6.4 Administrator
Full platform administrator.

Typical permissions:
- All moderator capabilities.
- Manage users and roles.
- Manage roads and road segments.
- Manage emergency services.
- Manage lookup/configuration data where appropriate.
- Manage safety-score settings.
- View administrative analytics and audit history.
- Perform system-level maintenance.

### 6.5 External Providers
Not human users, but supporting integrations.

Potential providers:
- Map tile/geocoding provider.
- Routing provider.
- Weather provider.
- Optional traffic provider.
- Optional email/notification provider.

Provider-specific logic must be isolated behind interfaces/services so the application is not tightly coupled to one vendor.

---

## 7. Core User Journeys

### Journey A — Find a Safer Route

1. User opens the map.
2. User selects a start location and destination.
3. System geocodes or resolves both locations.
4. Routing provider returns one or more candidate routes.
5. Candidate route geometry is matched to SafePath BD road segments where possible.
6. Latest road-segment safety scores are collected.
7. Route-level safety is calculated.
8. System compares candidate routes.
9. User sees:
   - Safest route
   - Fastest route
   - Shortest route
   - Distance
   - Estimated travel time
   - Overall safety score
   - Important warnings
10. User may save a preferred route.

### Journey B — Report a Hazard

1. Logged-in user chooses **Report Hazard**.
2. System obtains or lets the user select a location.
3. User chooses a hazard type.
4. User enters title, description, risk level, and optional photo.
5. A parent row is created in `reports`.
6. A hazard subtype row is created in `hazard_reports`.
7. Report status begins as `PENDING`.
8. Other users may confirm/dispute or comment.
9. Moderator/Admin reviews the report.
10. Report becomes verified/rejected/duplicate/etc.
11. Verified active hazards may influence safety scoring.

### Journey C — Report an Accident

1. Logged-in user chooses **Report Accident**.
2. User provides location, accident type, severity, time, casualties/vehicles if known, description, and optional image.
3. A parent row is created in `reports`.
4. An accident subtype row is created in `accident_reports`.
5. Moderator/Admin reviews the report.
6. If verified and suitable, a trusted record may be created in `accidents`.
7. The verified accident can then contribute to road-segment risk.

### Journey D — Emergency Assistance

1. User provides current location or selects a point.
2. System searches active emergency services near that location.
3. Results may include:
   - Hospital
   - Police Station
   - Fire Service
   - Ambulance
   - Emergency Center
4. Map and contact information are displayed.
5. The application does **not** claim to dispatch an emergency service unless such an official integration is later built.

---

## 8. Central Safety Concept

The project is built around **road segments**, not only whole roads.

A road may be divided into several segments because different sections of the same road can have different safety characteristics.

Conceptual flow:

```text
Verified Accidents
        +
Active Verified Hazards
        +
Road Condition
        +
Traffic Condition
        +
Weather Condition
        +
Lighting Quality
        ↓
Road Segment Risk Evaluation
        ↓
Road Segment Safety Score (0–100)
        ↓
Candidate Route Segment Scores
        ↓
Overall Route Safety Score
        ↓
Safest / Fastest / Shortest Comparison
```

The database currently contains configurable default weights:

| Factor | Default Weight |
|---|---:|
| Historical accident risk | 0.35 |
| Hazard risk | 0.25 |
| Road condition risk | 0.15 |
| Traffic risk | 0.10 |
| Weather risk | 0.10 |
| Lighting risk | 0.05 |

These are initial academic-project defaults stored in `system_settings`. They are configurable and should not be hardcoded throughout the codebase.

---

## 9. Scope of Version 1

Version 1 should include:

- Responsive ASP.NET Core MVC website.
- User registration/login/logout.
- Role-based authorization.
- Interactive map.
- Location search/geocoding.
- Route request and route display.
- Accident and hazard reports.
- Images, votes, comments.
- Moderator/Admin report review.
- Road and road-segment data management.
- Safety-score calculation.
- Safest/fastest/shortest route comparison.
- Emergency service search.
- User notifications stored in database.
- Saved places and saved routes.
- Basic analytics/dashboard.

---

## 10. Explicit Non-Goals for Version 1

Do not overbuild these initially:

- No official ambulance/police dispatch.
- No autonomous emergency decision-making.
- No guarantee of real-time government traffic feeds.
- No continuous background GPS tracking.
- No native Android/iOS application.
- No machine-learning accident prediction requirement.
- No automatic modification of the existing database schema.
- No microservice architecture.
- No distributed event bus or complex cloud infrastructure.
- No requirement to implement every future integration before the core project works.

These can become future enhancements.

---

## 11. Recommended Technology Direction

Keep the initial project simple and maintainable:

- **Backend/Web:** ASP.NET Core MVC
- **Language:** C#
- **Views:** Razor (`.cshtml`)
- **Database:** MySQL 8.x
- **ORM:** Entity Framework Core with a MySQL-compatible provider
- **Authentication:** Cookie-based authentication using the existing `users`, `roles`, and `user_roles` tables
- **Frontend:** HTML, CSS, Bootstrap, JavaScript
- **Map UI:** Leaflet
- **Map Data:** OpenStreetMap or another configured map provider
- **Routing:** External routing service wrapped behind an application interface
- **Weather:** Optional external weather service wrapped behind an interface
- **API Documentation:** Swagger/OpenAPI for JSON endpoints
- **Logging:** ASP.NET Core logging
- **Testing:** Unit tests for core services; integration tests for important flows

---

## 12. Source-of-Truth Rules

1. The existing MySQL database `safepath_bd` is the database source of truth.
2. Never invent a table or column without checking `DATABASE_SCHEMA.md` and the SQL schema.
3. Do not rename or remove existing tables/columns unless explicitly approved.
4. Do not run EF migrations that alter the schema during the initial database-first setup.
5. Business rules belong in services, not Razor views or controllers.
6. External APIs must be wrapped behind interfaces.
7. User-submitted reports are not automatically considered verified data.
8. Safety scoring must remain explainable; factor contributions should be traceable.
9. Location history is consent-based and action-specific, not continuous tracking.
10. Security, validation, privacy, and auditability are first-class requirements.

---

## 13. Definition of Success

The first complete academic version is successful when a user can:

- Register and log in.
- Open the map.
- Select a start and destination.
- See candidate route information.
- See a safety score and safety-related warnings.
- Submit accident and hazard reports.
- See verified reports on the map.
- Find nearby emergency services.
- Save places/routes.
- Receive system notifications.

And an administrator/moderator can:

- Review and verify reports.
- Maintain essential road/emergency data.
- View safety and reporting statistics.
- Update configurable safety settings.
- Audit administrative actions.

That is the functional identity of SafePath BD.
