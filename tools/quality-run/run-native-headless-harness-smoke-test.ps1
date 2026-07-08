$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$smokeRoot = Join-Path $repoRoot 'artifacts\quality-run\native-headless-smoke'
$capturedDir = Join-Path $smokeRoot 'captured'
$materializedDir = Join-Path $smokeRoot 'materialized'
$manifestPath = Join-Path $smokeRoot 'manifest.json'
$summaryPath = Join-Path $smokeRoot 'materialized-summary.json'
$validationPath = Join-Path $smokeRoot 'validation.json'
$analysisPath = Join-Path $smokeRoot 'analysis.json'
$reportPath = Join-Path $capturedDir 'jellyfin\sdr-hevc-main10-1080p60-3m.json'
$materializedReportPath = Join-Path $materializedDir 'jellyfin\sdr-hevc-main10-1080p60-3m.json'
$sampleUrl = 'https://repo.jellyfin.org/test-videos/SDR/HEVC%2010bit/Test%20Jellyfin%201080p%20HEVC%2010bit%203M.mp4'

if (Test-Path $smokeRoot) {
    $resolvedSmokeRoot = (Resolve-Path $smokeRoot).Path
    if (-not $resolvedSmokeRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove smoke output outside repo root: $resolvedSmokeRoot"
    }

    Remove-Item -LiteralPath $smokeRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $smokeRoot | Out-Null

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Headless\NextGenEmby.PlaybackQuality.Headless.csproj') -- `
    --case-id jellyfin/sdr-hevc-main10-1080p60-3m `
    --stream-url $sampleUrl `
    --duration-seconds 5 `
    --reports-dir $capturedDir
if ($LASTEXITCODE -ne 0) {
    throw 'Expected native-headless harness command to return exit code 0.'
}

if (-not (Test-Path $reportPath)) {
    throw "Expected native headless harness report at $reportPath."
}

$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json

if ($report.caseMetadata.caseId -ne 'jellyfin/sdr-hevc-main10-1080p60-3m') {
    throw "Expected caseMetadata.caseId to match requested case id."
}

if ($report.report.runId -ne 'jellyfin/sdr-hevc-main10-1080p60-3m') {
    throw "Expected report.runId to match requested case id."
}

if ($report.report.result -notin @('skip', 'error', 'pass')) {
    throw "Expected report.result to be skip, error, or pass."
}

if ($report.report.environment.collectorVersion -ne 'native-headless-harness-v0.1') {
    throw "Expected native headless collector version."
}

if ([string]::IsNullOrWhiteSpace($report.report.environment.playerCoreVersion)) {
    throw "Expected playerCoreVersion to be present."
}

if ([string]::IsNullOrWhiteSpace($report.report.environment.sourceRevision)) {
    throw "Expected sourceRevision to be present."
}

if ($report.report.result -eq 'skip') {
    if ($report.report.skip.code -ne 'native-headless.native-link-blocked') {
        throw "Expected structured native-headless linkage blocker while real native open is not wired."
    }

    if ($report.report.skip.failureClass -ne 'insufficient instrumentation') {
        throw "Expected initial structured skip to be classified as insufficient instrumentation."
    }

    if ($report.report.skip.reason -notmatch 'Windows Store C\+\+/WinRT' -or
        $report.report.skip.reason -notmatch 'SwapChainPanel') {
        throw "Expected native-headless blocker reason to name the current native linkage and surface dependency."
    }

    if ($report.report.limitations -contains 'native-headless: command shape exists, but real App-free native open is not wired yet') {
        throw "Headless harness must now report the exact native linkage blocker, not the initial command-shape limitation."
    }

    if (-not ($report.report.limitations -contains 'native-headless: current NextGenEmby.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection')) {
        throw "Expected explicit native-headless WinRT linkage limitation."
    }

    if (-not ($report.report.limitations -contains 'native-headless: real App-free playback evidence requires a native graph host or render-surface abstraction before this runner can open PlaybackGraph')) {
        throw "Expected explicit native-headless next decoupling limitation."
    }
}

@"
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "jellyfin/sdr-hevc-main10-1080p60-3m",
      "category": "stable",
      "severity": "high",
      "stability": "stable",
      "uri": "$sampleUrl",
      "purpose": [
        "sdr-smoke",
        "frame-pacing"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 1080,
        "frameRate": 60.0,
        "hdrKind": "Sdr",
        "isDirectPlayable": true,
        "maxStartupDurationMs": 2000,
        "minRenderedVideoFrames": 120
      }
    }
  ]
}
"@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    materialize-native-harness-report-set `
    --manifest $manifestPath `
    --captured-reports-dir $capturedDir `
    --reports-dir $materializedDir `
    --collector-version native-headless-harness-v0.1 `
    --player-core-version smoke-core `
    --source-revision smoke-native-headless-import-revision `
    --build-configuration Debug `
    --output $summaryPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialize-native-harness-report-set to import native-headless captured report.'
}

if (-not (Test-Path $materializedReportPath)) {
    throw "Expected materialized native-headless report at $materializedReportPath."
}

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    validate-report-set `
    --manifest $manifestPath `
    --reports-dir $materializedDir `
    --output $validationPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected materialized native-headless report-set to pass validation.'
}

$validation = Get-Content -LiteralPath $validationPath -Raw | ConvertFrom-Json
if ($validation.isValid -ne $true -or $validation.matchedCaseCount -ne 1) {
    throw 'Expected materialized native-headless validation to match the captured case.'
}

dotnet run --project (Join-Path $repoRoot 'tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj') -- `
    analyze-report-set `
    --reports-dir $materializedDir `
    --output $analysisPath
if ($LASTEXITCODE -ne 0) {
    throw 'Expected analyze-report-set to consume materialized native-headless report-set.'
}

$analysis = Get-Content -LiteralPath $analysisPath -Raw | ConvertFrom-Json
if ($analysis.playbackEvidence.canEvaluateNativePlayback -ne $false) {
    throw 'Skip-only native-headless blocker report must not be treated as native playback evidence.'
}

if (-not ($analysis.limitations -contains 'native-headless: current NextGenEmby.Native build is a Windows Store C++/WinRT component with public playback entrypoints bound to UWP projection')) {
    throw 'Expected analyze-report-set summary to preserve native-headless linkage limitation.'
}

Write-Host 'native-headless-harness smoke ok'
