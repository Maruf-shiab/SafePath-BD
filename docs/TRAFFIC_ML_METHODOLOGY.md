# SafePath BD Traffic ML Methodology

## Purpose

Chunk 7 treats traffic estimation as a **regression** problem. The target is `congestion_index_0_100`. The model does not predict safety, accidents, hazards, vehicle ETA, or the final recommended journey. Those are downstream systems.

The supplied AUST 10 km dataset is **synthetic development data**. It is suitable for prototyping and academic model comparison, but it must not be described as measured, official, live, or real-time Dhaka traffic.

## Dataset

- File: `SafePathBD.Web/Data/Traffic/aust_10km_synthetic_traffic_development.csv`
- Rows: 33,768
- Development roads: 201
- Temporal pattern: 7 days × 24 hours per road
- Primary target: `congestion_index_0_100`
- Derived display class: LOW / MODERATE / HEAVY / SEVERE

## Leakage-safe feature contract

Production candidates use these raw feature groups:

- `road_id`
- `road_class`
- `area`
- `day_type`
- `weather_condition`
- `road_condition_score_0_100`
- `distance_from_aust_km`
- `HourSin`, `HourCos`
- `DaySin`, `DayCos`

Cyclic features are calculated as:

`HourSin = sin(2π × hour / 24)`

`HourCos = cos(2π × hour / 24)`

`DaySin = sin(2π × dayIndex / 7)`

`DayCos = cos(2π × dayIndex / 7)`

The following target-derived fields are explicitly excluded from the ML feature vector:

- `traffic_level`
- `db_traffic_level`
- all `avg_speed_*` fields
- all `travel_min_per_km_*` fields

Those speed columns are used only **after** congestion prediction to calibrate deterministic vehicle travel-time relationships.

## Models

The training console project compares:

1. FastTree Regression — nonlinear boosted tree ensemble.
2. LightGBM Regression — nonlinear gradient-boosted tree ensemble.
3. SDCA Regression — efficient linear comparison model. Its feature vector is normalized and deterministic options disable per-iteration shuffle and restrict thread count.

A simple road-class/hour historical mean is also evaluated as a baseline.

FastTree and LightGBM each use a **small deterministic three-configuration tuning set** on the same validation partition. Only the best validation configuration from each family enters the final family-level comparison. SDCA uses one normalized deterministic linear configuration. This is intentionally bounded rather than a large AutoML search.

## Reproducibility

`TrafficFeatureBuilder.Seed = 24091` is used throughout the experiment. The partition itself does not depend on runtime random shuffling.

## Primary split

The primary experiment uses a stable FNV-1a hash of:

`road_id | day_index | hour`

Buckets:

- 0–69: train (~70%)
- 70–84: validation (~15%)
- 85–99: test (~15%)

Every model receives the **same exact records** for each partition. Test data is not used to select a model.

## Road-holdout robustness evaluation

A deterministic subset of complete `road_id` values is excluded from training and used only as a secondary road-holdout test. It answers how model behavior changes on roads not explicitly seen during fitting. These robustness metrics are reported separately and are not the sole production selection criterion because the current SafePath traffic prototype is primarily in-domain around its known road catalog.

## Metrics

Primary regression metrics:

- MAE
- RMSE
- R²

Secondary traffic-class metrics after converting predictions to LOW/MODERATE/HEAVY/SEVERE:

- Accuracy
- Macro Precision
- Macro Recall
- Macro F1

Percentage error is intentionally not a primary metric because congestion can approach zero.

## Automatic production selection

The production winner is selected only from validation results:

1. lowest validation MAE;
2. if MAE differs by no more than `0.01`, lower validation RMSE;
3. if still tied, higher validation R²;
4. if still tied, the simpler/faster model.

Metrics must be finite. The final inference result is clamped to 0–100, while excessive out-of-range raw predictions are recorded as a quality warning via clamp rate.

After the winner is frozen, the untouched test set is evaluated. Experimental model artifacts are preserved and the selected artifact is copied to:

`SafePathBD.Web/ML/Models/traffic-congestion-model.zip`

## Permutation feature importance

The selected model receives grouped raw-feature permutation importance on validation data. The report measures the increase in MAE after independently permuting each feature group. This represents predictive association, **not causal proof**.

## Runtime fallback hierarchy

The web app never trains during startup or a route request. If the canonical artifact is missing or cannot load, traffic prediction falls back in this order:

1. exact road + hour + day-type synthetic mean;
2. road + hour mean;
3. road-class + hour mean;
4. global hourly mean.

The response carries the prediction source and a HIGH/MEDIUM/LOW data-confidence label. Missing ML never silently becomes congestion `0`.

## Training command

From the solution root:

```powershell
dotnet restore
dotnet run --project SafePathBD.ML.Training
```

The command writes the selected model, experimental models, metadata, metrics JSON, comparison CSV, and `docs/TRAFFIC_MODEL_COMPARISON.md`.

## Limitations

- The dataset is synthetic and centered on the AUST development area.
- Road coordinates/catalog are approximations, not an exhaustive authoritative OSM road extract.
- The initial weather input is configuration/default context rather than a live weather feed.
- Production deployment should retrain on authoritative traffic observations and a full road-segment mapping.
