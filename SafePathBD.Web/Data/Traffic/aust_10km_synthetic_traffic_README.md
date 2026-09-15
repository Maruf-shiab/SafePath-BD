# AUST 10 km Synthetic Traffic Development Dataset

- AUST reference center: `23.76361, 90.40694`
- Generated road segments: **201**
- Temporal coverage: **7 days × 24 hours**
- Total traffic records: **33,768**
- Traffic values are **synthetic**, intended for SafePath BD ML/routing prototyping.
- `congestion_index_0_100` is the primary ML regression target; `traffic_level` is the derived classification label.
- Vehicle-specific speeds and travel minutes/km are provided for car, motorbike, rickshaw, bus, and walking.
- Bus availability/wait time is only a synthetic road-level approximation; true multimodal bus routing needs a separate stop/route/schedule network.
- Road coordinates and road catalog are development approximations, not an authoritative exhaustive OpenStreetMap extract.
- For a genuinely exhaustive “every small road within 10 km” production dataset, import all OSM `highway=*` ways in a 10 km radius, split them into routable segments, and generate the same hourly features for each segment.

Suggested model inputs:
`road_id`, `road_class`, `area`, `day_of_week`, `hour`, `weather_condition`, `road_condition_score_0_100`

Suggested targets:
- Regression: `congestion_index_0_100`
- Classification: `traffic_level`

SafePath usage:
1. ML predicts congestion for current hour/road segment.
2. Convert prediction to vehicle-specific expected speed/travel time.
3. Combine with verified hazards and segment safety.
4. Build multimodal edge costs.
5. Return top-3 route options.

