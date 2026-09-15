# Traffic model artifacts

Run `dotnet run --project SafePathBD.ML.Training` from the solution root.

The training project generates:

- `traffic-congestion-model.zip` — selected canonical production artifact.
- `traffic-model-metadata.json` — selected model, feature/split contract and final metrics.
- `traffic-model-metrics.json` — experiment metrics.
- `traffic-model-comparison.csv` — model comparison.
- `experiments/fasttree-model.zip`
- `experiments/lightgbm-model.zip`
- `experiments/sdca-model.zip`

No pre-trained artifact is committed/generated in this package because training must be executed by ML.NET and this build environment does not provide the .NET SDK. The web runtime safely uses the documented synthetic-pattern fallback hierarchy until the canonical artifact exists.
