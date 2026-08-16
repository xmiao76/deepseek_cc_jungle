# Fails when the Core line coverage in the newest cobertura report is below the
# gate (80%). The test project also references JungleGame.Bench (arena protocol
# tests), so the rate is computed over JungleGame.Core classes only.
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

$totalLines = 0
$coveredLines = 0
foreach ($class in @($xml.coverage.packages.package.classes.class)) {
    if ($class.filename -notlike '*JungleGame.Core*') { continue }
    foreach ($line in @($class.lines.line)) {
        $totalLines++
        if ([int]$line.hits -gt 0) { $coveredLines++ }
    }
}

if ($totalLines -eq 0) {
    Write-Error "No JungleGame.Core lines found in the coverage report."
    exit 1
}

$lineRate = $coveredLines / $totalLines

Write-Host "Coverage report: $($report.FullName)"
Write-Host "Core line rate: $($lineRate.ToString('P1')) (gate: $($MinimumLineRate.ToString('P0')))"

if ($lineRate -lt $MinimumLineRate) {
    Write-Error "Line coverage $($lineRate.ToString('P1')) is below the gate of $($MinimumLineRate.ToString('P0'))."
    exit 1
}
