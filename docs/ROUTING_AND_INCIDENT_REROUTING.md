# SafePath BD — Routing and Incident-Aware Rerouting

## Purpose

Chunk 6 adds the first complete road-routing foundation to SafePath BD. It deliberately does **not** implement the later Safety Score Engine or a `SAFEST` route. The feature compares real provider road routes by distance and estimated duration, then overlays current **public, VERIFIED** SafePath accident/hazard information to warn users and recommend a suitable alternative when the shortest route is seriously affected.

## Architecture

```text
Browser map workspace
    ↓
POST /api/v1/routes/search
    ↓
IRoutingService / RoutingService
    ↓
IRoutingProvider
    ↓
OsrmRoutingProvider
    ↓
OSRM road router
    ↓
Provider-neutral route candidates
    ↓
IRouteIncidentService / RouteIncidentService
    ↓
public + VERIFIED SafePath reports
    ↓
Shortest / Fastest / incident state / recommendation
    ↓
Short-lived IMemoryCache entry
    ↓
Map route cards + Leaflet route layers
```

The browser never receives or controls an OSRM base URL. Provider configuration exists only on the server. This keeps the integration replaceable and avoids SSRF-style client-controlled provider requests.

## OSRM configuration

Configuration lives in `appsettings.json` under `Routing:OSRM`:

- `BaseUrl` — OSRM server base URL.
- `Profile` — currently `driving`.
- `TimeoutSeconds` — provider timeout.
- `CacheMinutes` — request-scoped route search cache lifetime.
- `IncidentRecheckSeconds` — selected-route incident refresh interval.
- `IncidentProximityMeters` — maximum report-to-route distance used for route incident matching.
- `DetourFallbackEnabled` — whether bounded via-point fallback is allowed.
- `DetourOffsetMeters` — base perpendicular via-point offset.
- `MaxDetourAttempts` — hard cap on fallback provider calls.
- `MaxDetourDistanceFactor` — maximum allowed detour distance relative to the original shortest route.
- `CautionAlternativeDistanceFactor` — maximum reasonable distance multiplier when offering a clear route instead of a caution-level shortest route.

The default public endpoint `https://router.project-osrm.org/` is suitable for development and an academic demonstration. It is not a production SLA. A production deployment should point `BaseUrl` at an appropriate managed or self-hosted routing service.

## Source and destination flow

`map.js` remains the owner of the existing start/destination behavior:

- Nominatim autocomplete.
- reverse geocoding.
- map-click selection.
- Start/Destination markers.
- Swap.
- browser geolocation.

Chunk 6 exposes only a small `window.SafePathMapWorkspace` API so `routing.js` can read the selected endpoints, use the existing drawer, fit map bounds, and request one current-location fix. It does **not** initialize another Leaflet map.

Whenever either endpoint changes, existing route results, polylines and incident recheck timers are invalidated. This prevents stale route information from remaining visible for old coordinates.

The existing My Location workflow was also corrected so the first successful location request can immediately become the Start point; a second click is no longer required.

## Route search API

`POST /api/v1/routes/search`

Example request:

```json
{
  "start": {
    "latitude": 23.7639,
    "longitude": 90.4064,
    "label": "AUST"
  },
  "destination": {
    "latitude": 23.7557,
    "longitude": 90.3755,
    "label": "Dhanmondi"
  }
}
```

The server validates latitude/longitude ranges, rejects effectively identical endpoints, and reads `system_settings.default_route_search_radius_km` to prevent unbounded route searches.

## OSRM request details

The provider calls:

```text
route/v1/driving/{longitude},{latitude};{longitude},{latitude}
```

with:

```text
alternatives=true
overview=full
geometries=geojson
steps=false
```

For detours, a temporary via point is inserted between start and destination and alternatives are disabled for that provider call.

### GeoJSON coordinate order

OSRM/GeoJSON uses:

```text
[longitude, latitude]
```

SafePath internal DTOs and Leaflet rendering use explicit named fields:

```text
Latitude
Longitude
```

`OsrmRoutingProvider` performs the conversion once at the integration boundary to avoid coordinate reversal elsewhere in the application.

## Distance and duration

OSRM returns:

- distance in metres.
- duration in seconds.

SafePath keeps those source units and exposes convenience values:

- `DistanceMeters`
- `DistanceKm`
- `DurationSeconds`
- `DurationMinutes`

The UI calls duration **Estimated time**. It is not described as live traffic time because the current OSRM setup is not a live-traffic provider.

## Shortest algorithm

`SHORTEST` is the provider candidate with the minimum `DistanceMeters`.

Tie-breaking order:

1. lower distance.
2. lower duration.
3. provider order.

Incident status never changes which route is technically shortest.

## Fastest algorithm

`FASTEST` is the provider candidate with the minimum `DurationSeconds`.

Tie-breaking order:

1. lower duration.
2. lower distance.
3. provider order.

The same candidate may display both `SHORTEST` and `FASTEST`.

## No Safest route yet

Chunk 6 does not calculate:

- route safety score.
- road-segment matching coverage.
- traffic risk.
- weather risk.
- lighting risk.
- road-condition risk.
- accident hotspot weight.

Therefore the UI never labels any route `SAFEST`.

## Incident data source

Current route guidance reads:

- `reports`
- `report_statuses`
- `locations`
- `accident_reports`
- `accident_severities`
- `hazard_reports`
- `hazard_types`

Only reports where:

```text
status_code = VERIFIED
AND is_public = true
```

are eligible.

`PENDING`, `UNDER_REVIEW`, `REJECTED`, `DUPLICATE`, `NEEDS_INFO`, and `RESOLVED` reports are excluded from active route guidance.

The historical `accidents` table is **not** used to infer a current blockage. It remains trusted historical/safety-engine data for later phases.

## Incident proximity algorithm

The routing phase occurs before full road-segment matching, so matching is geometric:

1. Calculate each candidate route bounding box.
2. Expand it by `IncidentProximityMeters`.
3. Query only public VERIFIED reports inside that expanded box using `AsNoTracking()` and projection.
4. For each returned report, calculate shortest point-to-polyline distance.
5. Keep reports within the configured proximity threshold.

`RouteGeometryMath` uses Haversine for great-circle operations and a local equirectangular projection for point-to-short-segment projection. It also provides route bounding boxes, bearing, closest-point lookup, and geodesic offset-coordinate generation.

## Incident classification

Route incident states are intentionally different from a future safety score.

### CLEAR

No relevant current public VERIFIED incident was detected within the configured route proximity.

### CAUTION

A lower/moderate verified incident was detected.

Accidents:

- Minor → CAUTION
- Moderate → CAUTION

Hazards:

- LOW → CAUTION
- MODERATE → CAUTION
- HIGH → CAUTION

### AFFECTED

A serious verified route issue was detected. It does not claim that the road is physically impassable.

Accidents:

- Severe → AFFECTED
- Fatal → AFFECTED

Hazards:

- CRITICAL → AFFECTED
- Road Block + HIGH → AFFECTED
- Road Block + CRITICAL → AFFECTED

The policy is centralized in `RouteIncidentPolicy`.

## Recommended alternative algorithm

The technical preferred route begins as `SHORTEST`.

- If Shortest is `CLEAR`, it remains the recommendation.
- If Shortest is `CAUTION`, a `CLEAR` candidate may be offered when its distance is within `CautionAlternativeDistanceFactor`.
- If Shortest is `AFFECTED`, choose the shortest `CLEAR` normal candidate.
- If none is clear, choose the shortest `CAUTION` normal candidate.
- If every normal candidate is `AFFECTED`, attempt the bounded detour fallback.

The UI uses `Recommended alternative` / `Shortest suitable alternative`; it never calls this the Safest Route.

## Recommendation trade-off

When the recommendation differs from Shortest, SafePath calculates:

```text
recommended distance - shortest distance
recommended duration - shortest duration
```

The route panel displays the real extra/reduced kilometres and minutes returned by OSRM.

## Detour fallback

OSRM does not know SafePath's report database and does not natively exclude a SafePath incident. When all normal alternatives are AFFECTED, SafePath can generate bounded temporary via points:

1. Pick the highest-priority AFFECTED incident on the shortest route.
2. Find the closest point on route geometry.
3. Estimate local route bearing.
4. Generate perpendicular candidate bearings (`+90°`, `-90°`).
5. Offset a temporary via point by `DetourOffsetMeters`.
6. Ask OSRM for Start → Via → Destination.
7. Run the same VERIFIED incident check on returned OSRM road geometry.
8. Reject any detour still classified AFFECTED.
9. Reject near-duplicate alternatives.
10. Reject a detour beyond `MaxDetourDistanceFactor`.
11. Stop after `MaxDetourAttempts`.

Attempts alternate sides first, then widen the offset by 50% per pair. A CAUTION detour is retained only as a fallback while searching for a CLEAR detour.

No straight-line route is ever displayed. The temporary via point only influences OSRM; the final line shown on the map is actual OSRM road geometry.

## Short-lived route cache

Candidate routes are not inserted into `routes` or `route_segments` because the existing `routes.route_type` enum only supports `SAFEST`, `FASTEST`, and `SHORTEST`. Neutral OSRM alternatives and incident detours would corrupt those semantics.

A successful search receives a cryptographically random `Guid` `SearchId`. `IMemoryCache` stores only the request-scoped endpoint/candidate geometry needed for incident rechecks and expires it after `CacheMinutes`.

Authenticated searches are bound to the authenticated user ID for rechecks. Anonymous searches rely on the short-lived opaque GUID.

This cache is not permanent history and does not write `user_location_history`.

## Periodic incident recheck

While a route is selected, `routing.js` calls:

`POST /api/v1/routes/recheck`

using only:

- `SearchId`
- `SelectedCandidateKey`

The browser does not resend route geometry. The server retrieves geometry from the short-lived cache and reevaluates current public VERIFIED reports.

Rechecks:

- run at `IncidentRecheckSeconds`.
- pause while `document.hidden` is true.
- stop when endpoints change.
- stop when route results are cleared/page unloads.
- do **not** call OSRM again.

If a selected route changes from Clear/Caution to Affected, the UI shows a controlled high-visibility alert and offers:

- Find alternative.
- Re-route from my current location.
- Keep current route.

The application never silently changes the route.

## Find alternative

`Find alternative` reruns the normal route search using the original current Start/Destination. This allows OSRM alternatives and SafePath incident checks to be recomputed using the latest verified report state.

## Re-route from current location

The button requests one browser location fix through `navigator.geolocation.getCurrentPosition()`, applies that coordinate as Start, and performs a new route search to the existing Destination.

Chunk 6 explicitly does not use `watchPosition()`, background GPS tracking, movement trails, voice navigation, or turn-by-turn navigation.

## Privacy

Routing does not expose:

- reporter name/ID.
- email/phone.
- moderator notes.
- voter identities.
- private report evidence.

It does not persist source/destination route searches as user history and does not write active route coordinates to `user_location_history`.

Incident output contains only route-relevant public report data: report type/title, severity/risk/type, report coordinate/location label, reported time, and approximate distance from route.

## Error behavior

The API distinguishes:

- invalid endpoint input → HTTP 400.
- no drivable provider route → HTTP 422.
- provider timeout/unavailability → HTTP 503.
- expired route-search cache → HTTP 410 on recheck.

If incident checking fails after valid provider routes were obtained, the routes remain visible and are marked with unavailable incident information rather than being discarded.

A failed periodic recheck also keeps the currently displayed route.

## UI behavior

- `Find routes` activates only when both endpoints exist.
- all OSRM candidates are drawn.
- selected route uses the strongest neutral/accent line treatment.
- other candidates use lower opacity.
- Shortest and Fastest badges are independent and may appear together.
- incident state is textual (`Clear`, `Caution`, `Affected`) and not color-only.
- relevant selected-route incident markers reuse SafePath report marker language.
- route cards are keyboard-selectable.
- route alerts use `aria-live`.
- the existing map drawer becomes the route panel on desktop and the existing bottom sheet on mobile.
- mobile route results initially use a compact selected-route summary (route, km, estimated time, labels/status) that expands to all alternatives; a newly AFFECTED selected route automatically expands so the warning is not hidden.
- reduced-motion preferences disable unnecessary route/alert motion while preserving functionality.

## Provider limitations

The public OSRM demo service can be unavailable, rate-limited, or unsuitable for production traffic. SafePath treats its duration as a routing estimate, not a live-traffic ETA guarantee.

This phase provides route planning plus verified-incident reevaluation. It is not continuous navigation.

## Future Safety Intelligence compatibility

The current provider-neutral route candidate structure can later be extended with fields such as:

- road-segment matching coverage.
- route safety score.
- safety coverage/confidence.
- `IsSafest`.

Those features intentionally remain unimplemented until the later Safety Intelligence phase.
