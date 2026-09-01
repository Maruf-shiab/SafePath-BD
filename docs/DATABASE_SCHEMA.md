# SafePath BD — Database Schema

## 1. Database Status

**Database Name:** `safepath_bd`  
**Database Engine:** MySQL 8.x / InnoDB  
**Status:** Already created successfully.  
**Schema Strategy:** Database-first.

> **Critical instruction:** The existing MySQL database is the source of truth. Do not recreate, rename, drop, or alter tables/columns through Entity Framework migrations unless the user explicitly approves a schema change.

The schema contains:

- **35 tables**
- **3 database views**
- **4 data-integrity triggers**
- Foreign keys, indexes, unique constraints, check constraints, and seed lookup data

---

## 2. Module Summary

| Module | Tables |
|---|---|
| User & Access | `users`, `roles`, `user_roles` |
| Location & Roads | `locations`, `roads`, `road_segments`, `user_location_history`, `saved_places` |
| Report Lookups | `report_statuses`, `accident_types`, `accident_severities`, `hazard_types` |
| Community Reporting | `reports`, `accident_reports`, `hazard_reports`, `report_images`, `report_votes`, `report_comments`, `report_verifications` |
| Verified Accident Data | `accidents` |
| Road Risk Inputs | `road_conditions`, `traffic_conditions`, `weather_conditions` |
| Safety Engine | `safety_scores`, `safety_score_factors` |
| Routing | `routes`, `route_segments`, `saved_routes` |
| Emergency Services | `emergency_service_types`, `emergency_services` |
| Notifications | `notification_types`, `notifications` |
| Administration | `admin_actions`, `feedback`, `system_settings` |

---

# 3. Table-by-Table Description

## 3.1 `users`

**Purpose:** Stores registered application users.

Important fields:
- `user_id` — PK
- `full_name`
- `email` — unique
- `phone` — unique when present
- `password_hash`
- `profile_image_url`
- `is_active`
- `email_verified`
- `last_login_at`
- timestamps

Relationships:
- 1 user → many role assignments
- 1 user → many reports
- 1 user → many votes/comments
- 1 user → many saved places/routes
- 1 user → many notifications
- 1 user → many admin actions when acting as admin
- 1 user → many feedback records

Do not expose `password_hash`.

---

## 3.2 `roles`

**Purpose:** Defines application roles.

Seed roles:
- Admin
- Moderator
- User

Relationship:
- many users ↔ many roles through `user_roles`

---

## 3.3 `user_roles`

**Purpose:** Bridge table for the users-to-roles many-to-many relationship.

FKs:
- `user_id` → `users.user_id`
- `role_id` → `roles.role_id`

Unique pair:
- `(user_id, role_id)`

---

## 3.4 `locations`

**Purpose:** Reusable map coordinates and resolved place information.

Important fields:
- `location_id` — PK
- `latitude`
- `longitude`
- `address_line`
- `landmark_name`
- `area_name`
- `city`
- `district`
- `division_name`
- `country`
- `place_provider`
- `external_place_id`

Used by:
- road segments
- reports
- accidents
- routes
- saved places
- emergency services

`place_provider` supports values such as GOOGLE, OSM, MANUAL, OTHER.

---

## 3.5 `roads`

**Purpose:** Master record for a named road.

Important fields:
- `road_id`
- `road_code`
- `road_name`
- `road_type`
- `city`
- `district`
- `default_speed_limit_kmh`
- `is_active`

Relationship:
- 1 road → many road segments

---

## 3.6 `road_segments`

**Purpose:** Divides roads into smaller units for safety scoring and route analysis.

Important fields:
- `road_segment_id`
- `road_id`
- `start_location_id`
- `end_location_id`
- `segment_name`
- `distance_km`
- `average_travel_time_min`
- `speed_limit_kmh`
- `lane_count`
- `is_one_way`
- `encoded_polyline`
- `is_active`

FKs:
- `road_id` → `roads`
- `start_location_id` → `locations`
- `end_location_id` → `locations`

Used by:
- reports
- verified accidents
- road conditions
- traffic conditions
- weather conditions
- safety scores
- route segments
- notifications

This is one of the core entities of the project.

---

## 3.7 `user_location_history`

**Purpose:** Stores optional, consent-based action locations.

Examples of purpose:
- ROUTE_REQUEST
- ACCIDENT_REPORT
- HAZARD_REPORT
- EMERGENCY
- OTHER

Important note:
This table is **not** intended for continuous user tracking.

---

## 3.8 `saved_places`

**Purpose:** Lets a user save named locations.

Types:
- HOME
- OFFICE
- UNIVERSITY
- FAVORITE
- OTHER

FKs:
- user
- location

---

# 4. Report Lookup Tables

## 4.1 `report_statuses`

**Purpose:** Controls report lifecycle.

Seed statuses:
- PENDING
- UNDER_REVIEW
- VERIFIED
- REJECTED
- RESOLVED
- DUPLICATE
- NEEDS_INFO

---

## 4.2 `accident_types`

**Purpose:** Defines accident categories and default risk weight.

Seed examples:
- Vehicle Collision
- Pedestrian Accident
- Motorcycle Accident
- Bus Accident
- Truck Accident
- Rollover
- Hit and Run
- Other

---

## 4.3 `accident_severities`

**Purpose:** Defines accident severity and risk weight.

Seed values:
- Minor — 1.00
- Moderate — 2.00
- Severe — 4.00
- Fatal — 5.00

---

## 4.4 `hazard_types`

**Purpose:** Defines road-hazard categories and default risk weight.

Seed examples:
- Pothole
- Broken Road
- Waterlogging
- Broken Traffic Signal
- Road Construction
- Illegal Parking
- Poor Street Lighting
- Fallen Tree
- Road Block
- Dangerous Intersection
- Debris on Road
- Other

---

# 5. Unified Community Reporting

## 5.1 `reports`

**Purpose:** Parent table shared by all accident/hazard reports.

Important fields:
- `report_id`
- `report_type` — ACCIDENT or HAZARD
- `user_id`
- `location_id`
- `road_segment_id`
- `status_id`
- `title`
- `description`
- `is_public`
- `reported_at`
- `resolved_at`

FKs:
- user
- location
- optional road segment
- report status

Why this parent exists:
Images, votes, comments, verifications, and notifications can all reference one valid report FK instead of using unsafe polymorphic IDs.

---

## 5.2 `accident_reports`

**Purpose:** Accident-specific data for a parent `reports` row.

Relationship:
- `reports` 1 ↔ 1 `accident_reports` for an accident report

Primary/FK:
- `report_id` → `reports.report_id`

Additional FKs:
- accident type
- severity

Important fields:
- `accident_occurred_at`
- `number_of_vehicles`
- `number_of_injured`
- `number_of_deaths`
- `weather_notes`

A database trigger ensures the parent report has `report_type = 'ACCIDENT'`.

---

## 5.3 `hazard_reports`

**Purpose:** Hazard-specific data for a parent `reports` row.

Relationship:
- `reports` 1 ↔ 1 `hazard_reports` for a hazard report

Primary/FK:
- `report_id` → `reports.report_id`

Important fields:
- `hazard_type_id`
- `risk_level`
- `observed_at`
- `expected_clearance_at`

Risk levels:
- LOW
- MODERATE
- HIGH
- CRITICAL

A database trigger ensures the parent report has `report_type = 'HAZARD'`.

---

## 5.4 `report_images`

**Purpose:** Stores one or more image references for a report.

Relationship:
- 1 report → many images

Important:
Store path/URL metadata in the database, not raw large image bytes unless explicitly redesigned later.

---

## 5.5 `report_votes`

**Purpose:** Community confirmation/dispute system.

Vote types:
- CONFIRM
- DISPUTE

Relationships:
- report → many votes
- user → many votes

A user should have at most one vote per report according to the database's unique constraint.

---

## 5.6 `report_comments`

**Purpose:** Discussion on community reports.

Relationships:
- report → many comments
- user → many comments
- optional self-reference via `parent_comment_id` for replies

Supports soft deletion through `is_deleted`.

---

## 5.7 `report_verifications`

**Purpose:** Stores moderation/admin verification history.

Relationships:
- report → many verification events
- admin user → many verification events
- status → many verification events

Keep history instead of overwriting all previous decisions.

---

# 6. Verified Accident Data

## 6.1 `accidents`

**Purpose:** Stores trusted/historical accident records used by the safety engine.

This is **different from `accident_reports`**.

Important fields:
- `accident_id`
- optional `source_report_id`
- location
- optional road segment
- accident type
- severity
- occurred time
- vehicle count
- injury count
- death count
- weather condition
- description
- verified by / verified time

If created from a community report:
- `source_report_id` should reference an ACCIDENT report.

A trigger validates that rule.

---

# 7. Road Risk Input Tables

## 7.1 `road_conditions`

**Purpose:** Stores measured/entered road quality for a segment.

Fields include:
- surface condition
- surface score
- lighting score
- drainage score
- visibility score
- overall condition score
- source type
- recorded by
- recorded at

Surface conditions:
- EXCELLENT
- GOOD
- MODERATE
- POOR
- DANGEROUS

---

## 7.2 `traffic_conditions`

**Purpose:** Stores traffic/congestion state for a road segment.

Traffic levels:
- LOW
- MODERATE
- HEAVY
- SEVERE

Fields:
- average speed
- congestion score
- source
- recorded time

---

## 7.3 `weather_conditions`

**Purpose:** Stores weather-related risk for a road segment.

Fields include:
- weather type
- temperature
- rainfall
- visibility
- weather risk score
- source
- recorded time

---

# 8. Safety Engine Tables

## 8.1 `safety_scores`

**Purpose:** Stores calculated road-segment safety score.

Important fields:
- `road_segment_id`
- `overall_safety_score`
- `risk_level`
- `methodology_version`
- `calculated_at`
- `valid_until`

Safety score range is intended to be 0–100.

---

## 8.2 `safety_score_factors`

**Purpose:** Explain exactly how a safety score was produced.

Factor types:
- ACCIDENT
- HAZARD
- ROAD_CONDITION
- TRAFFIC
- WEATHER
- LIGHTING
- OTHER

Fields:
- raw value
- normalized risk
- factor weight
- weighted risk
- details

Relationship:
- 1 safety score → many factor rows

This table makes the safety recommendation explainable.

---

# 9. Route Tables

## 9.1 `routes`

**Purpose:** Stores generated route alternatives.

Route types:
- SAFEST
- FASTEST
- SHORTEST

Important fields:
- optional user
- start location
- destination location
- distance
- estimated duration
- overall safety score
- encoded polyline
- generation/expiry timestamps

---

## 9.2 `route_segments`

**Purpose:** Bridge between a route and the road segments it contains.

FKs:
- `route_id`
- `road_segment_id`

Important:
- `sequence_no` stores traversal order.
- segment-specific distance/duration/safety can be stored.

Logical relationship:
- routes M:N road_segments through `route_segments`

This is the second major many-to-many relationship in the database.

---

## 9.3 `saved_routes`

**Purpose:** Lets a user save a generated route using a custom name.

FKs:
- user
- route

---

# 10. Emergency Service Tables

## 10.1 `emergency_service_types`

Seed types:
- Hospital
- Police Station
- Fire Service
- Ambulance
- Emergency Center

---

## 10.2 `emergency_services`

**Purpose:** Stores emergency facilities.

Important fields:
- service type
- location
- service name
- phone
- emergency phone
- opening hours
- 24-hour flag
- website
- verified flag
- active flag

Relationship:
- type 1:M emergency services
- location 1:M emergency services

---

# 11. Notification Tables

## 11.1 `notification_types`

Seed examples:
- HAZARD_ALERT
- ACCIDENT_ALERT
- EMERGENCY_ALERT
- REPORT_VERIFIED
- REPORT_REJECTED
- REPORT_RESOLVED
- ROAD_RISK_ALERT
- SYSTEM

---

## 11.2 `notifications`

**Purpose:** In-app user notifications.

Can reference:
- report
- route
- road segment

Fields include:
- title
- message
- `is_read`
- created/read timestamps

---

# 12. Administration and Configuration

## 12.1 `admin_actions`

**Purpose:** Audit trail for administrative actions.

Fields:
- admin user
- action type
- entity type
- entity id
- description
- metadata JSON
- timestamp

---

## 12.2 `feedback`

**Purpose:** User feedback/complaints/suggestions.

Status:
- OPEN
- IN_REVIEW
- RESOLVED
- CLOSED

Optional rating is stored.

---

## 12.3 `system_settings`

**Purpose:** Configurable application behavior.

Important seeded settings:

| Key | Value |
|---|---:|
| `safety_weight_accident` | 0.35 |
| `safety_weight_hazard` | 0.25 |
| `safety_weight_road_condition` | 0.15 |
| `safety_weight_traffic` | 0.10 |
| `safety_weight_weather` | 0.10 |
| `safety_weight_lighting` | 0.05 |
| `safety_score_methodology_version` | 1.0 |
| `report_vote_confirmation_threshold` | 3 |
| `default_route_search_radius_km` | 25 |
| `safety_score_cache_minutes` | 15 |

Do not hardcode these settings in multiple services.

---

# 13. Major Relationships

## Many-to-Many

### Users ↔ Roles

```text
users
  1
  │
  M
user_roles
  M
  │
  1
roles
```

Logical relationship:
`users M:N roles`

### Routes ↔ Road Segments

```text
routes
  1
  │
  M
route_segments
  M
  │
  1
road_segments
```

Logical relationship:
`routes M:N road_segments`

---

## Important One-to-Many Relationships

- `roads` 1:M `road_segments`
- `users` 1:M `reports`
- `locations` 1:M `reports`
- `report_statuses` 1:M `reports`
- `reports` 1:M `report_images`
- `reports` 1:M `report_votes`
- `reports` 1:M `report_comments`
- `reports` 1:M `report_verifications`
- `road_segments` 1:M `accidents`
- `road_segments` 1:M `road_conditions`
- `road_segments` 1:M `traffic_conditions`
- `road_segments` 1:M `weather_conditions`
- `road_segments` 1:M `safety_scores`
- `safety_scores` 1:M `safety_score_factors`
- `users` 1:M `notifications`
- `emergency_service_types` 1:M `emergency_services`

---

## Important One-to-One/Subtype Relationships

- `reports` 1:0..1 `accident_reports`
- `reports` 1:0..1 `hazard_reports`

A report must use the subtype matching its `report_type`.

---

# 14. Database Views

## `vw_report_overview`

Provides a convenient combined report view containing:
- report information
- status
- reporter name
- location
- road segment
- confirm vote count
- dispute vote count

Use for report dashboards/listing when appropriate.

## `vw_latest_segment_safety_score`

Returns latest calculated safety score per road segment.

Useful for route evaluation.

## `vw_emergency_services_with_location`

Returns emergency service information together with latitude/longitude and address fields.

Useful for map markers and nearby service search.

---

# 15. Database Triggers

The database contains four important triggers:

1. `trg_accident_reports_validate_type_insert`
   - Accident subtype requires parent type ACCIDENT.

2. `trg_hazard_reports_validate_type_insert`
   - Hazard subtype requires parent type HAZARD.

3. `trg_reports_prevent_type_change`
   - Prevents changing report type after a subtype exists.

4. `trg_accidents_validate_source_report_insert`
   - If a verified accident references a source report, that report must be ACCIDENT.

The application should respect these rules before the database rejects invalid data.

---

# 16. EF Core Rules

When connecting ASP.NET Core:

- Use the existing database schema.
- Preserve exact snake_case table and column names.
- Correctly map unsigned MySQL numeric fields.
- Correctly map ENUM values.
- Do not expose entity models directly to all views/APIs.
- Use ViewModels/DTOs.
- Use `AsNoTracking()` for read-only queries.
- Keep navigation properties consistent with FKs.
- Avoid cascade assumptions that contradict the actual SQL.
- Do not create schema-changing migrations automatically.

---

# 17. Database Integrity Rule for Copilot

Before writing any query or entity:

1. Find the table in this file.
2. Confirm its fields/FKs.
3. Use existing names exactly.
4. Do not create imaginary columns such as `RiskScore` if the actual column is `overall_safety_score`.
5. If a required field does not exist, stop and document the need instead of silently changing the schema.
