# SafePath BD — Development Plan

## 1. Development Strategy

SafePath BD should be built in small verified phases.

Do **not** ask an AI coding assistant to build the entire project in a single prompt.

Every phase should end with:
- code builds,
- relevant page/API works,
- database interaction is verified,
- no known critical error is ignored.

---

# Phase 0 — Documentation & Repository Foundation

## Goal
Establish project rules before generating application code.

## Deliverables
- `/docs` contains all seven documentation files.
- Existing MySQL SQL schema is preserved under `/database`.
- `.gitignore` created.
- README created later if needed.

## Acceptance
- AI can explain project goal, architecture, database-first rule, and development phases correctly.

---

# Phase 1 — Create Minimal ASP.NET Core MVC Project

## Goal
Create only the minimal project skeleton.

## Deliverables
- ASP.NET Core MVC application.
- Basic Bootstrap/Razor template.
- Environment-specific configuration.
- Basic exception handling.
- Project builds.

## Do Not Build Yet
- full map
- reporting
- safety engine
- route engine
- admin dashboard

## Acceptance
- `dotnet build` succeeds.
- home page loads.

---

# Phase 2 — MySQL / EF Core Integration

## Goal
Connect the existing `safepath_bd` database.

## Tasks
- Add required EF Core/MySQL provider.
- Add connection configuration.
- Create/scaffold `SafePathDbContext`.
- Map existing tables.
- Preserve schema.
- Test simple read query.
- Confirm views/triggers still exist.

## Acceptance
- App connects to MySQL.
- No schema migration is automatically executed.
- Existing tables remain unchanged.
- At least one seeded lookup can be read.

---

# Phase 3 — Authentication & Roles

## Goal
Implement secure user access using existing tables.

## Tasks
- Register.
- Secure password hashing.
- Login.
- Logout.
- Cookie auth.
- Default User role.
- Load role claims.
- Admin/Moderator authorization.
- Profile basics.

## Acceptance
- New user can register.
- Password is stored only as hash.
- User can login/logout.
- Admin-only route cannot be accessed by User role.

---

# Phase 4 — Base UI & Navigation

## Goal
Build a professional reusable layout.

## Deliverables
- Landing page.
- Navigation bar.
- Footer.
- Auth-aware navigation.
- User dashboard shell.
- Admin Area shell.

## Acceptance
- Responsive layout works.
- No business features are faked as completed.

---

# Phase 5 — Interactive Map Foundation

## Goal
Show a working map.

## Tasks
- Add Leaflet.
- Base map.
- Current location permission.
- Map click.
- Start/destination markers.
- Location search/reverse geocoding provider abstraction.
- Map JSON endpoints.

## Acceptance
- Map loads.
- User can select coordinates.
- Backend receives valid coordinates.
- Invalid coordinate input is rejected.

---

# Phase 6 — Emergency Services

## Goal
Deliver an early practical map feature.

## Tasks
- Read emergency service view/table.
- Map emergency markers.
- Nearby search using coordinates.
- Filter by type.
- Details/contact display.

## Acceptance
- Nearby services are returned from database.
- Distance is calculated correctly.
- No fake "dispatch" behavior.

---

# Phase 7 — Accident & Hazard Reporting

## Goal
Implement the community reporting core.

## Tasks
- Accident report form.
- Hazard report form.
- Parent `reports` transaction.
- Correct subtype insert.
- Image upload.
- My Reports.
- Report details.
- Public visibility rules.

## Acceptance
- Correct parent/subtype records are created.
- DB triggers are satisfied.
- Invalid subtype data does not save.
- Initial status is PENDING.

---

# Phase 8 — Community Interaction

## Goal
Add validation signals.

## Tasks
- Confirm/dispute.
- Comments.
- Replies.
- Vote count.
- Soft delete behavior if implemented.

## Acceptance
- One user cannot create duplicate conflicting vote rows.
- Comments load safely.
- Deleted comments do not expose removed content.

---

# Phase 9 — Moderator/Admin Report Verification

## Goal
Turn community reports into managed information.

## Tasks
- Verification queue.
- Status transitions.
- Verification comment/history.
- Notify reporter.
- Audit admin action.
- For verified accident report, support creating trusted `accidents` record.

## Acceptance
- Every review creates history.
- Current report status updates.
- Reporter receives notification.
- Unauthorized user cannot verify.

---

# Phase 10 — Road & Road Segment Administration

## Goal
Maintain data required for route safety.

## Tasks
- Roads CRUD.
- Road segments CRUD.
- Segment geometry/polyline.
- Start/end locations.
- Road condition records.

## Acceptance
- Road/segment data can be managed by authorized admin.
- FK validation is respected.

---

# Phase 11 — Safety Score Engine

## Goal
Implement the main risk algorithm.

## Tasks
- Read weights from `system_settings`.
- Calculate accident factor.
- Calculate hazard factor.
- Calculate road condition factor.
- Calculate traffic factor.
- Calculate weather factor.
- Calculate lighting factor.
- Normalize 0–100.
- Store `safety_scores`.
- Store `safety_score_factors`.
- Add unit tests.

## Acceptance
- Same input produces deterministic score.
- Score remains 0–100.
- Factor weights are loaded from DB.
- Factor rows explain final score.
- Tests pass.

---

# Phase 12 — Route Provider Integration

## Goal
Generate candidate routes.

## Tasks
- Implement `IRoutingProvider`.
- Configure provider.
- Request alternatives.
- Parse distance/duration/polyline.
- Handle provider errors.
- Do not expose secrets.

## Acceptance
- Candidate route(s) display on map.
- Failure is handled cleanly.

---

# Phase 13 — Safe Route Comparison

## Goal
Combine routing with SafePath safety data.

## Tasks
- Match candidate routes to road segments where practical.
- Load latest segment safety.
- Calculate route safety.
- Persist route and route segments.
- Identify:
  - safest
  - fastest
  - shortest
- Display comparison.
- Explain risk factors/warnings.

## Acceptance
- Recommendation is based on data, not hardcoded.
- Safest may differ from shortest.
- If route data is incomplete, UI communicates confidence/limitations.

---

# Phase 14 — Saved Places & Saved Routes

## Goal
Improve usability.

## Tasks
- Save Home/Office/University/Favorite.
- Save generated route.
- List/delete saved entries as appropriate.

## Acceptance
- Users can access only their own saved data.

---

# Phase 15 — Notifications

## Goal
Complete in-app notification flow.

## Tasks
- Notification center.
- Unread count.
- Mark read.
- Create notifications from report verification and selected safety events.

## Acceptance
- User only sees own notifications.

---

# Phase 16 — Admin Dashboard & Analytics

## Goal
Provide administrative overview.

## Suggested metrics
- users
- pending reports
- verified/rejected reports
- hazards by type
- accidents by severity
- high-risk segments
- recent admin actions

## Acceptance
- Metrics come from real database data.
- Charts/tables do not use invented production statistics.

---

# Phase 17 — Hardening

## Security
- antiforgery
- file upload validation
- cookie settings
- authorization audit
- secret audit
- error handling

## Performance
- pagination
- map bounding-box filtering
- no N+1 queries
- read-only `AsNoTracking`
- external API timeout/caching

## Accessibility/UX
- labels
- keyboard access where practical
- responsive map
- clear risk terminology

---

# Phase 18 — Testing

## Unit Tests
- safety score
- route comparison
- report workflow
- permissions/business rules

## Integration Tests
- database connection
- report creation
- login/auth
- admin verification
- critical APIs

---

# Phase 19 — Final Documentation & Demo Preparation

Prepare:
- README
- installation guide
- database setup guide
- environment configuration guide
- architecture explanation
- demo accounts created securely
- test/demo data
- screenshots
- presentation workflow

Recommended demo scenario:

1. Login as user.
2. Open map.
3. Find route.
4. Compare safest vs shortest.
5. Report hazard.
6. Login as moderator/admin.
7. Verify hazard.
8. Recalculate/view safety effect.
9. Find emergency service.
10. Show analytics.

---

# Development Stop-Gate Rule

An AI assistant must not continue blindly across phases.

After each phase:

```text
Implement
   ↓
Build
   ↓
Test
   ↓
Fix
   ↓
Summarize changes
   ↓
Proceed only when foundation is stable
```

If the database connection, authentication, or current module is broken, fix it before adding later features.
