$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-quality-cli-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $baselinePath = Join-Path $tempRoot 'baseline.json'
    $candidatePath = Join-Path $tempRoot 'candidate.json'
    $baselineEnvelopePath = Join-Path $tempRoot 'baseline-envelope.json'
    $candidateEnvelopePath = Join-Path $tempRoot 'candidate-envelope.json'
    $outputPath = Join-Path $tempRoot 'comparison.json'
    $envelopeOutputPath = Join-Path $tempRoot 'comparison-envelope.json'
    $suitePath = Join-Path $tempRoot 'suite.json'
    $baselineDir = Join-Path $tempRoot 'baseline-suite'
    $candidateDir = Join-Path $tempRoot 'candidate-suite'
    $comparisonsDir = Join-Path $tempRoot 'suite-comparisons'
    $suiteFromReportsPath = Join-Path $tempRoot 'suite-from-reports.json'
    $stallBaselineDir = Join-Path $tempRoot 'stall-baseline-suite'
    $stallCandidateDir = Join-Path $tempRoot 'stall-candidate-suite'
    $previousComparisonsDir = Join-Path $tempRoot 'previous-comparisons'
    $stallComparisonsDir = Join-Path $tempRoot 'stall-suite-comparisons'
    $stallSuitePath = Join-Path $tempRoot 'stall-suite.json'

    @'
{
  "runId": "baseline",
  "metricVersion": "software-quality-v1",
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "frameRate": 23.976,
    "hdrKind": "Sdr"
  },
  "checks": [
    {
      "name": "MaxFrameGapMs",
      "signal": "timing.maxFrameGapMs",
      "status": "fail",
      "failureArea": "frame-pacing",
      "expected": "105.000",
      "actual": "180.000"
    }
  ]
}
'@ | Set-Content -LiteralPath $baselinePath -Encoding UTF8

    @'
{
  "runId": "candidate",
  "metricVersion": "software-quality-v1",
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "frameRate": 23.976,
    "hdrKind": "Sdr"
  },
  "checks": [
    {
      "name": "MaxFrameGapMs",
      "signal": "timing.maxFrameGapMs",
      "status": "fail",
      "failureArea": "frame-pacing",
      "expected": "105.000",
      "actual": "120.000"
    }
  ]
}
'@ | Set-Content -LiteralPath $candidatePath -Encoding UTF8

    $baselineReportJson = Get-Content -Raw -LiteralPath $baselinePath
    $candidateReportJson = Get-Content -Raw -LiteralPath $candidatePath

    @"
{
  "report": $baselineReportJson,
  "modelAnalysis": {}
}
"@ | Set-Content -LiteralPath $baselineEnvelopePath -Encoding UTF8

    @"
{
  "report": $candidateReportJson,
  "modelAnalysis": {}
}
"@ | Set-Content -LiteralPath $candidateEnvelopePath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
            --baseline $baselinePath `
            --candidate $candidatePath `
            --output $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $outputPath)) {
        throw 'Expected playback quality CLI to write comparison output.'
    }

    $comparison = Get-Content -Raw -LiteralPath $outputPath | ConvertFrom-Json
    if ($comparison.result -ne 'improved') {
        throw 'Expected playback quality CLI comparison result to be improved.'
    }

    if ($comparison.confidence.level -ne 'strong') {
        throw 'Expected playback quality CLI comparison confidence to be strong.'
    }

    if ($comparison.optimization.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI optimization action to accept candidate.'
    }

    if (-not ($comparison.improvements | Where-Object { $_.signal -eq 'timing.maxFrameGapMs' })) {
        throw 'Expected playback quality CLI comparison to include timing.maxFrameGapMs improvement.'
    }

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
            --baseline $baselineEnvelopePath `
            --candidate $candidateEnvelopePath `
            --output $envelopeOutputPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI envelope comparison returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $envelopeComparison = Get-Content -Raw -LiteralPath $envelopeOutputPath | ConvertFrom-Json
    if ($envelopeComparison.result -ne 'improved') {
        throw 'Expected playback quality CLI envelope comparison result to be improved.'
    }

    if (-not ($envelopeComparison.improvements | Where-Object { $_.signal -eq 'timing.maxFrameGapMs' })) {
        throw 'Expected playback quality CLI envelope comparison to include timing.maxFrameGapMs improvement.'
    }

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- summarize `
            --comparison $outputPath `
            --output $suitePath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI summarize returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $suitePath)) {
        throw 'Expected playback quality CLI to write suite output.'
    }

    $suite = Get-Content -Raw -LiteralPath $suitePath | ConvertFrom-Json
    if ($suite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI suite action to accept candidate.'
    }

    if ($suite.totalComparisonCount -ne 1) {
        throw 'Expected playback quality CLI suite to include one comparison.'
    }

    New-Item -ItemType Directory -Path $baselineDir | Out-Null
    New-Item -ItemType Directory -Path $candidateDir | Out-Null
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $baselineDir 'case-a.json')
    Copy-Item -LiteralPath $candidateEnvelopePath -Destination (Join-Path $candidateDir 'case-a.json')

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare-suite `
            --baseline-dir $baselineDir `
            --candidate-dir $candidateDir `
            --comparisons-dir $comparisonsDir `
            --output $suiteFromReportsPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI compare-suite returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $suiteFromReportsPath)) {
        throw 'Expected playback quality CLI compare-suite to write suite output.'
    }

    $suiteFromReports = Get-Content -Raw -LiteralPath $suiteFromReportsPath | ConvertFrom-Json
    if ($suiteFromReports.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI compare-suite action to accept candidate.'
    }

    if ($suiteFromReports.totalComparisonCount -ne 1) {
        throw 'Expected playback quality CLI compare-suite to include one comparison.'
    }

    if (-not ($suiteFromReports.cases | Where-Object { $_.caseId -eq 'case-a.json' -and $_.action -eq 'accept-candidate' })) {
        throw 'Expected playback quality CLI compare-suite to include case summary for case-a.json.'
    }

    $comparisonFromSuitePath = Join-Path $comparisonsDir 'case-a.json'
    if (-not (Test-Path -LiteralPath $comparisonFromSuitePath)) {
        throw 'Expected playback quality CLI compare-suite to write individual comparison output.'
    }

    $comparisonFromSuite = Get-Content -Raw -LiteralPath $comparisonFromSuitePath | ConvertFrom-Json
    if ($comparisonFromSuite.result -ne 'improved') {
        throw 'Expected playback quality CLI compare-suite comparison result to be improved.'
    }

    if ($comparisonFromSuite.caseId -ne 'case-a.json') {
        throw 'Expected playback quality CLI compare-suite comparison to include caseId.'
    }

    New-Item -ItemType Directory -Path $stallBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $stallCandidateDir | Out-Null
    New-Item -ItemType Directory -Path $previousComparisonsDir | Out-Null
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $stallBaselineDir 'case-stall.json')
    Copy-Item -LiteralPath $baselineEnvelopePath -Destination (Join-Path $stallCandidateDir 'case-stall.json')

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare `
            --baseline $baselineEnvelopePath `
            --candidate $baselineEnvelopePath `
            --output (Join-Path $previousComparisonsDir 'case-stall.json')
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI previous comparison generation returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare-suite `
            --baseline-dir $stallBaselineDir `
            --candidate-dir $stallCandidateDir `
            --previous-comparisons-dir $previousComparisonsDir `
            --comparisons-dir $stallComparisonsDir `
            --stall-threshold 2 `
            --output $stallSuitePath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI compare-suite with previous comparisons returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $stallSuite = Get-Content -Raw -LiteralPath $stallSuitePath | ConvertFrom-Json
    if ($stallSuite.action -ne 'change-optimization-strategy') {
        throw 'Expected playback quality CLI compare-suite stall action to change optimization strategy.'
    }

    $stallComparison = Get-Content -Raw -LiteralPath (Join-Path $stallComparisonsDir 'case-stall.json') | ConvertFrom-Json
    if ($stallComparison.optimization.action -ne 'change-optimization-strategy') {
        throw 'Expected playback quality CLI compare-suite stall comparison action to change optimization strategy.'
    }

    if (-not ($stallComparison.optimization.blockers | Where-Object { $_ -eq 'iteration.stalled' })) {
        throw 'Expected playback quality CLI compare-suite stall comparison to include iteration.stalled blocker.'
    }

    Write-Output 'playback-quality-cli smoke ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
