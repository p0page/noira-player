$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-quality-cli-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $baselinePath = Join-Path $tempRoot 'baseline.json'
    $candidatePath = Join-Path $tempRoot 'candidate.json'
    $outputPath = Join-Path $tempRoot 'comparison.json'
    $suitePath = Join-Path $tempRoot 'suite.json'
    $baselineDir = Join-Path $tempRoot 'baseline-suite'
    $candidateDir = Join-Path $tempRoot 'candidate-suite'
    $comparisonsDir = Join-Path $tempRoot 'suite-comparisons'
    $suiteFromReportsPath = Join-Path $tempRoot 'suite-from-reports.json'

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
    Copy-Item -LiteralPath $baselinePath -Destination (Join-Path $baselineDir 'case-a.json')
    Copy-Item -LiteralPath $candidatePath -Destination (Join-Path $candidateDir 'case-a.json')

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

    $comparisonFromSuitePath = Join-Path $comparisonsDir 'case-a.json'
    if (-not (Test-Path -LiteralPath $comparisonFromSuitePath)) {
        throw 'Expected playback quality CLI compare-suite to write individual comparison output.'
    }

    $comparisonFromSuite = Get-Content -Raw -LiteralPath $comparisonFromSuitePath | ConvertFrom-Json
    if ($comparisonFromSuite.result -ne 'improved') {
        throw 'Expected playback quality CLI compare-suite comparison result to be improved.'
    }

    Write-Output 'playback-quality-cli smoke ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
