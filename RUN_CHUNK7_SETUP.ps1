$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE. Fix this error before continuing."
    }
}

Write-Host 'SafePath BD — Chunk 7 build / ML training / verification' -ForegroundColor Cyan
Write-Host 'Run this script from the repository root with .NET 8 SDK installed.'

if (-not (Test-Path '.\SafePathBD.sln')) {
    throw 'SafePathBD.sln was not found. Run this script from the SafePath-BD project root.'
}

Write-Host "`n[0/7] Clearing Mark-of-the-Web flags where Windows permits it..." -ForegroundColor Yellow
try {
    Get-ChildItem -Path . -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
} catch {
    Write-Warning 'Windows did not allow one or more files to be unblocked. Continuing; App Control may still require an allow policy.'
}

Write-Host "`n[1/7] Checking .NET SDK..." -ForegroundColor Yellow
Invoke-DotNet @('--version')

Write-Host "`n[2/7] Restoring solution..." -ForegroundColor Yellow
Invoke-DotNet @('restore', '.\SafePathBD.sln')

Write-Host "`n[3/7] Building source before training..." -ForegroundColor Yellow
Invoke-DotNet @('build', '.\SafePathBD.sln', '--no-restore')

Write-Host "`n[4/7] Running leakage-safe ML.NET experiment..." -ForegroundColor Yellow
try {
    Invoke-DotNet @('run', '--project', '.\SafePathBD.ML.Training\SafePathBD.ML.Training.csproj', '--no-build')
} catch {
    if ($_.Exception.Message -match '0x800711C7|Application Control policy') {
        throw @"
Windows Application Control blocked the locally built ML training assembly (0x800711C7).

Try these safe checks first:
1. Close Visual Studio/terminals using the project.
2. From the repository root run:
   Get-ChildItem -Recurse -File | Unblock-File
3. Remove generated output:
   Get-ChildItem -Recurse -Directory -Filter bin | Remove-Item -Recurse -Force
   Get-ChildItem -Recurse -Directory -Filter obj | Remove-Item -Recurse -Force
4. Run this setup script again.

If Windows still reports 0x800711C7, an active Windows App Control / Smart App Control / organization policy is blocking unsigned developer assemblies. Review Windows Security and Microsoft-Windows-CodeIntegrity/Operational logs or ask the device administrator to allow this locally built project. Do not bypass an organization-managed policy.
"@
    }
    throw
}

$required = @(
    '.\SafePathBD.Web\ML\Models\traffic-congestion-model.zip',
    '.\SafePathBD.Web\ML\Models\traffic-model-metadata.json',
    '.\SafePathBD.Web\ML\Models\traffic-model-metrics.json',
    '.\SafePathBD.Web\ML\Models\traffic-model-comparison.csv',
    '.\SafePathBD.Web\ML\Models\experiments\fasttree-model.zip',
    '.\SafePathBD.Web\ML\Models\experiments\lightgbm-model.zip',
    '.\SafePathBD.Web\ML\Models\experiments\sdca-model.zip'
)
foreach ($path in $required) {
    if (-not (Test-Path $path)) { throw "Required ML artifact was not generated: $path" }
}

Write-Host "`n[5/7] Rebuilding web/solution with generated model artifacts..." -ForegroundColor Yellow
Invoke-DotNet @('build', '.\SafePathBD.sln', '--no-restore')

Write-Host "`n[6/7] Running full regression + Chunk 7 tests..." -ForegroundColor Yellow
Invoke-DotNet @('test', '.\SafePathBD.sln', '--no-build')

Write-Host "`n[7/7] Verification complete." -ForegroundColor Green
Write-Host 'Start the application with:' -ForegroundColor Cyan
Write-Host '  dotnet run --project SafePathBD.Web'
Write-Host 'Then open the printed localhost URL and test the Intelligent Mobility panel on the map.'
