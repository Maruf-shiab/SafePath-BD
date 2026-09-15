# SafePath BD Traffic Model Comparison

**Status:** generated metrics are intentionally pending until the ML.NET training command is executed on a machine with the .NET 8 SDK.

The implementation does **not** hardcode FastTree, LightGBM, or SDCA as the winner and does not fabricate experiment metrics. Run:

```powershell
dotnet run --project SafePathBD.ML.Training
```

The training project will overwrite this file with the real validation/test/road-holdout comparison, baseline, selected production model, exact selection reason and permutation feature importance. The dataset is synthetic development data, so even the generated metrics must not be described as live-traffic performance.
