# Fails when the Core line coverage in the newest cobertura report is below the
# gate (80%). Only JungleGame.Core is referenced by the test project, so the
# aggregate report's line-rate is Core's line-rate.
param(
    [double]$MinimumLineRate = 0.80
)

$ErrorActionPreference = "Stop"

$report = Get-ChildItem -Path "JungleGame.Tests/TestResults" -Filter "coverage.cobertura.xml" -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $report) {
    Write-Error "No coverage.cobertura.xml found under JungleGame.Tests/TestResults. Run: dotnet test JungleGame.sln --collect:'XPlat Code Coverage'"
    exit 1
}

[xml]$xml = Get-Content $report.FullName
$lineRate = [double]$xml.coverage.'line-rate'

Write-Host "Coverage report: $($report.FullName)"
Write-Host "Line rate: $($lineRate.ToString('P1')) (gate: $($MinimumLineRate.ToString('P0')))"

if ($lineRate -lt $MinimumLineRate) {
    Write-Error "Line coverage $($lineRate.ToString('P1')) is below the gate of $($MinimumLineRate.ToString('P0'))."
    exit 1
}
