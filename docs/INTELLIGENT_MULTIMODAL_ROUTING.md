# SafePath BD Intelligent Multimodal Routing

## Scope

Chunk 7 extends, rather than replaces, the real Chunk 6 OSRM routing foundation. OSRM supplies valid road corridors and geometry. SafePath then overlays predicted traffic, vehicle-specific travel time, development transit data, verified incidents, available safety information, user mobility constraints, resilience, confidence, and explainable ranking.

Traffic and transit data in this build are **synthetic development data**. UI and API wording uses “predicted traffic”, “expected congestion”, and “synthetic development transit”; it never calls these values live traffic or official transit data.

## Request flow

`POST /api/v1/routes/intelligent`

Server-authoritative inputs:

- start / destination coordinates;
- departure time (`Leave Now` or custom);
- enabled modes: WALK, RICKSHAW, BUS, MOTORBIKE, CAR;
- preference profile;
- maximum walking distance;
- maximum transfers (0–2).

The browser does not supply a model path, CSV path, safety score, generalized cost, OSRM URL, ETA, congestion value, or ranking score.

## Architecture

1. Existing `IRoutingService` obtains real OSRM road candidates and verified incident context.
2. `IMultimodalGraphBuilder` samples those real road geometries into a request-scoped graph.
3. Road samples are matched to the nearest synthetic traffic-road context with explicit confidence; the mapping CSV never silently claims a database segment match where one is unconfirmed.
4. Synthetic development bus stops/routes are attached only when stops fall within the configured corridor threshold.
5. `ITimeDependentJourneyRouter` runs time-dependent Dijkstra over state `(node, mode, active bus route, transfers, walking bucket, arrival time)`.
6. `IKBestJourneyPlanner` uses a deterministic Yen-inspired bounded edge-suppression process plus useful single-mode searches to build a candidate pool.
7. The assembler calculates ETA, congestion, safety exposure, data confidence and resilience.
8. Generalized cost ranks candidates according to the selected preference profile.
9. A diversity filter suppresses near-duplicate top options when distinct candidates exist.
10. Explanations, backup-plan selection, departure-window analysis and what-if disruption simulation are added.

No route search is permanently stored by default.

## Time-dependent Dijkstra

Traffic is evaluated at the **arrival time of each edge**. A traveler leaving at 08:55 who reaches a later edge at 09:02 receives the traffic estimate for that later context. The original departure time is not reused for every segment.

The default graph is generated from real Chunk 6 provider geometry. The graph itself is short-lived and lives only for the current intelligent search/cache window.

## Vehicle-specific ETA

ML predicts congestion only. `IVehicleTravelTimeService` separately converts distance and predicted congestion into expected speed/time using development calibration derived from the CSV `avg_speed_*` columns. Those speed fields are not ML features.

- WALK: calibrated walking speed; largely traffic independent.
- RICKSHAW: calibrated speed; trunk roads are currently excluded by the development eligibility policy.
- MOTORBIKE: congestion sensitive, but typically less than car/bus in the supplied calibration.
- CAR: congestion-calibrated road travel.
- BUS: access walk + expected wait + boarding/transfer time + congestion-calibrated road movement + configured synthetic stop dwell + final access walk.

## Synthetic transit

`Data/Transit` contains explicitly labeled `SYNTHETIC_DEVELOPMENT` stops, routes, route-stop sequences and frequency windows. Expected wait is approximately half the configured headway. This is a prototype network, not authoritative Dhaka bus data.

## Transfer constraints

The router enforces:

- max transfers: 0, 1 or 2;
- max walking: 500 m, 1 km or 2 km from the UI (server accepts the configured range);
- practical transitions such as Rickshaw → Bus → Walk and Walk → Bus → Rickshaw;
- car/motorbike are treated as single-mode journeys in this phase.

## Traffic-road mapping and confidence

The supplied CSV `road_id` is **not assumed** to equal `road_segments.road_segment_id`. `Data/Traffic/traffic_road_mapping.csv` exists as the explicit bridge. Because the source SQL contains no seeded road-segment catalog to verify against, the current mapping rows are intentionally `UNMAPPED_DB_SEGMENT_NOT_SEEDED` instead of fabricated IDs.

Nearest-coordinate matching supplies traffic context for development routing, and match distance contributes to Route Data Confidence.

## Safety integration

Safety is deliberately behind `ISegmentSafetyProvider`.

- If a traffic road has an explicitly confirmed database segment mapping and a persisted `safety_scores` row, that score is consumed and adjusted for currently verified incidents.
- Where full segment safety coverage is unavailable, the service returns a clearly labeled `VERIFIED_INCIDENT_PROXY` rather than inventing a historical safety score.
- Proxy coverage is lower, which reduces Route Data Confidence.

This means the architecture is ready for the later full Safety Score Engine without embedding safety logic inside Dijkstra.

Only active **VERIFIED** public incident information from the established Chunk 6 incident pipeline influences current routing. Pending/rejected/duplicate/resolved/private reports are not exposed as active route conditions.

A verified `Road Block` with CRITICAL risk is treated as a hard block. Other serious verified incidents are strongly penalized rather than automatically asserted to be physically impassable.

## Safety Exposure

Each journey reports minutes by risk band:

- VeryHighRiskMinutes
- HighRiskMinutes
- ModerateRiskMinutes
- LowerRiskMinutes

This supplements an aggregate safety signal so two routes with similar averages can still differ by how long the traveler remains in elevated-risk sections.

## Route Data Confidence

Data confidence is **not** the probability that a route is safe. It combines:

- traffic prediction confidence/source;
- traffic-road match distance;
- safety-data coverage;
- verified-incident check availability.

Lower-quality fallbacks and unmapped safety data therefore reduce confidence instead of silently appearing authoritative.

## Route Resilience

`IRouteResilienceService` returns 0–100. Development factors include:

- bottleneck dependency;
- elevated-risk exposure;
- transfer fragility;
- hourly traffic volatility;
- number of viable alternatives.

Traffic volatility is derived from the supplied synthetic hourly patterns.

## Generalized cost and preference profiles

Balanced development weights are:

- safety: 45%
- travel-time efficiency: 25%
- congestion: 10%
- transfer convenience: 8%
- walking burden: 5%
- distance: 3%
- route resilience: 4%

Profiles `SAFETY_FIRST`, `FASTEST_PRACTICAL`, `LESS_WALKING`, and `FEWER_TRANSFERS` use centralized controlled variants. Unknown-data coverage adds a separate confidence penalty.

## Top 3 and diversity

The UI attempts to surface:

1. BEST OVERALL — lowest configured generalized cost;
2. FASTEST PRACTICAL — lowest predicted ETA when sufficiently distinct;
3. RESILIENT / LOW-TRANSFER — useful high-resilience/fewer-transfer candidate when sufficiently distinct.

Near duplicates are filtered around the centralized 90% similarity threshold where other distinct options exist. Similarity is computed from **actual route geometry proximity plus mode sequence**, not request-local edge IDs, so two OSRM alternatives that trace almost the same corridor are recognized as near duplicates. Labels are not forced when all candidates collapse to essentially the same journey.

## Backup route

A distinct candidate under the configured backup similarity threshold is retained as a BACKUP PLAN. It is computed from the same server-side candidate pool, not generated by the browser.

## Explainability

`IRouteExplanationService` generates deterministic text only from calculated values. It can explain:

- why an option was recommended;
- time difference;
- congestion difference;
- elevated-risk exposure difference;
- transfer difference;
- resilience difference.

No LLM is required. A textual claim is omitted when the corresponding measured difference is not material.

## Departure window

The selected journey is re-evaluated at:

- now/selected departure;
- +15 min;
- +30 min;
- +60 min.

Traffic is repredicted as each leg is reached. A “leave later” message appears only if improvement exceeds both centralized absolute and percentage thresholds.

## What-if disruption

`POST /api/v1/routes/intelligent/what-if` temporarily blocks the selected physical journey leg and reruns candidate search on the cached graph. The response is explicitly labeled **WHAT-IF SIMULATION** and never presented as a real road event.

## UI visualization

Three map modes are available:

- JOURNEY — line style by vehicle mode;
- TRAFFIC — line color by predicted congestion class/index;
- SAFETY — line color by current safety classification/proxy.

Vehicle modes use text/icons/line patterns as well as color so information is not color-only. Transfer points are marked. Mobile continues using the existing map drawer/bottom-sheet architecture.

## Privacy and security

- no continuous GPS;
- no automatic Home/Work inference;
- no permanent route search history by default;
- model/CSV/provider paths are server configuration only;
- coordinates, modes, time, transfer limit and walking limit are validated server-side;
- antiforgery protection remains enabled for POST APIs.

## Performance

- ML model loads once per process;
- traffic CSV and transit CSV catalogs load once and are indexed;
- traffic predictions are cached by road/time/weather context;
- no prediction HTTP call exists per segment;
- the graph and intelligent-search cache are short-lived.

## Current limitations

- traffic data are synthetic development patterns;
- transit data are synthetic development data;
- traffic-road mapping is proximity-based until authoritative road-segment IDs are populated/confirmed;
- full Safety Score Engine coverage is not yet available, so some legs use an explicit verified-incident proxy;
- no live traffic feed, continuous GPS, turn-by-turn directions, or voice navigation;
- no claim that synthetic predictions represent current Dhaka conditions.
