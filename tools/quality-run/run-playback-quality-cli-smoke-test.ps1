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
    $runIdBaselineDir = Join-Path $tempRoot 'runid-baseline-suite'
    $runIdCandidateDir = Join-Path $tempRoot 'runid-candidate-suite'
    $runIdComparisonsDir = Join-Path $tempRoot 'runid-suite-comparisons'
    $runIdSuitePath = Join-Path $tempRoot 'runid-suite.json'
    $candidateEvaluationManifestPath = Join-Path $tempRoot 'candidate-evaluation-manifest.json'
    $candidateEvaluationPath = Join-Path $tempRoot 'candidate-evaluation.json'
    $candidateEvaluationComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-comparisons'
    $candidateEvaluationInvalidCandidateDir = Join-Path $tempRoot 'candidate-evaluation-invalid-candidate'
    $candidateEvaluationInvalidPath = Join-Path $tempRoot 'candidate-evaluation-invalid.json'
    $candidateEvaluationInvalidComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-invalid-comparisons'
    $candidateEvaluationBlockedAnalysisCandidateDir = Join-Path $tempRoot 'candidate-evaluation-blocked-analysis-candidate'
    $candidateEvaluationBlockedAnalysisPath = Join-Path $tempRoot 'candidate-evaluation-blocked-analysis.json'
    $candidateEvaluationBlockedAnalysisComparisonsDir = Join-Path $tempRoot 'candidate-evaluation-blocked-analysis-comparisons'
    $baselineEvaluationBlockedAnalysisBaselineDir = Join-Path $tempRoot 'baseline-evaluation-blocked-analysis-baseline'
    $baselineEvaluationBlockedAnalysisPath = Join-Path $tempRoot 'baseline-evaluation-blocked-analysis.json'
    $baselineEvaluationBlockedAnalysisComparisonsDir = Join-Path $tempRoot 'baseline-evaluation-blocked-analysis-comparisons'
    $stallBaselineDir = Join-Path $tempRoot 'stall-baseline-suite'
    $stallCandidateDir = Join-Path $tempRoot 'stall-candidate-suite'
    $previousComparisonsDir = Join-Path $tempRoot 'previous-comparisons'
    $stallComparisonsDir = Join-Path $tempRoot 'stall-suite-comparisons'
    $stallSuitePath = Join-Path $tempRoot 'stall-suite.json'
    $manifestPath = Join-Path $tempRoot 'reference-manifest.json'
    $manifestValidationPath = Join-Path $tempRoot 'reference-manifest-validation.json'
    $runPlanPath = Join-Path $tempRoot 'reference-run-plan.json'
    $filteredRunPlanPath = Join-Path $tempRoot 'reference-run-plan-filtered.json'
    $embyRunPlanManifestPath = Join-Path $tempRoot 'emby-run-plan-manifest.json'
    $embyRunPlanPath = Join-Path $tempRoot 'emby-run-plan.json'
    $reportSetDir = Join-Path $tempRoot 'reference-report-set'
    $reportSetValidationPath = Join-Path $tempRoot 'reference-report-set-validation.json'

    @'
{
  "runId": "baseline",
  "metricVersion": "software-quality-v1",
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
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
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
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

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "netflix/chimera-4k-2398-hdr-pq",
      "uri": "https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4",
      "tier": 2,
      "purpose": [
        "hdr-output",
        "cadence-23.976"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Hdr10"
      }
    },
    {
      "caseId": "jellyfin/dv-profile5-hevc-4k",
      "uri": "https://example.invalid/jellyfin/dv-profile5-hevc-4k.mp4",
      "tier": 3,
      "purpose": [
        "dv-reject"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "DolbyVisionUnsupported"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-manifest `
            --manifest $manifestPath `
            --output $manifestValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI validate-manifest returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $manifestValidation = Get-Content -Raw -LiteralPath $manifestValidationPath | ConvertFrom-Json
    if ($manifestValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI validate-manifest output to be valid.'
    }

    if ($manifestValidation.caseCount -ne 2) {
        throw 'Expected playback quality CLI validate-manifest output to include two cases.'
    }

    if (-not ($manifestValidation.purposes | Where-Object { $_ -eq 'hdr-output' })) {
        throw 'Expected playback quality CLI validate-manifest output to include hdr-output purpose.'
    }

    if (-not ($manifestValidation.cases | Where-Object {
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.tier -eq 2 -and
        $_.expected.codec -eq 'hevc' -and
        $_.expected.hdrKind -eq 'Hdr10'
    })) {
        throw 'Expected playback quality CLI validate-manifest output to include schedulable case summary.'
    }

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- plan-runs `
            --manifest $manifestPath `
            --reports-dir captured-baseline `
            --duration 60 `
            --output $runPlanPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI plan-runs returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $runPlan = Get-Content -Raw -LiteralPath $runPlanPath | ConvertFrom-Json
    if ($runPlan.schemaVersion -ne 1) {
        throw 'Expected playback quality CLI plan-runs output schemaVersion 1.'
    }

    if ($runPlan.caseCount -ne 2) {
        throw 'Expected playback quality CLI plan-runs output to include two cases.'
    }

    if ($runPlan.durationSeconds -ne 60) {
        throw 'Expected playback quality CLI plan-runs output to keep requested duration.'
    }

    if (-not ($runPlan.cases | Where-Object {
        $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.runId -eq 'netflix/chimera-4k-2398-hdr-pq' -and
        $_.sourceUri -eq 'https://example.invalid/netflix/chimera-4k-2398-hdr-pq.mp4' -and
        $_.reportRelativePath -eq 'netflix/chimera-4k-2398-hdr-pq.json' -and
        $_.durationSeconds -eq 60 -and
        $_.expected.hdrKind -eq 'Hdr10'
    })) {
        throw 'Expected playback quality CLI plan-runs output to include a runnable HDR case.'
    }

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- plan-runs `
            --manifest $manifestPath `
            --reports-dir captured-hdr-smoke `
            --duration 60 `
            --purpose hdr-output `
            --max-tier 2 `
            --output $filteredRunPlanPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI filtered plan-runs returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $filteredRunPlan = Get-Content -Raw -LiteralPath $filteredRunPlanPath | ConvertFrom-Json
    if ($filteredRunPlan.caseCount -ne 1) {
        throw 'Expected filtered plan-runs output to include one planned case.'
    }

    if (-not ($filteredRunPlan.filters.purposes -contains 'hdr-output')) {
        throw 'Expected filtered plan-runs output to record purpose filter.'
    }

    if ($filteredRunPlan.filters.maxTier -ne 2) {
        throw 'Expected filtered plan-runs output to record max tier filter.'
    }

    if (-not ($filteredRunPlan.cases | Where-Object { $_.caseId -eq 'netflix/chimera-4k-2398-hdr-pq' })) {
        throw 'Expected filtered plan-runs output to include HDR case.'
    }

    if ($filteredRunPlan.cases | Where-Object { $_.caseId -eq 'jellyfin/dv-profile5-hevc-4k' }) {
        throw 'Expected filtered plan-runs output to exclude non-matching DV case.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "emby/007-hdr10",
      "uri": "emby://items/item-007",
      "itemId": "item-007",
      "mediaSourceId": "source-hdr10",
      "startPositionTicks": 123,
      "forceSdrOutput": true,
      "tier": 1,
      "purpose": [
        "hdr-output"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Hdr10"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $embyRunPlanManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- plan-runs `
            --manifest $embyRunPlanManifestPath `
            --reports-dir captured-emby `
            --duration 45 `
            --output $embyRunPlanPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI plan-runs returned a non-zero exit code for Emby item manifest.'
        }
    }
    finally {
        Pop-Location
    }

    $embyRunPlan = Get-Content -Raw -LiteralPath $embyRunPlanPath | ConvertFrom-Json
    if (-not ($embyRunPlan.cases | Where-Object {
        $_.caseId -eq 'emby/007-hdr10' -and
        $_.captureMode -eq 'emby-item' -and
        $_.devCommand.route -eq 'quality-run' -and
        $_.devCommand.itemId -eq 'item-007' -and
        $_.devCommand.mediaSourceId -eq 'source-hdr10' -and
        $_.devCommand.runId -eq 'emby/007-hdr10' -and
        $_.devCommand.durationSeconds -eq 45 -and
        $_.devCommand.startPositionTicks -eq 123 -and
        $_.devCommand.forceSdrOutput -eq $true -and
        $_.devCommand.expected.hdrKind -eq 'Hdr10'
    })) {
        throw 'Expected playback quality CLI plan-runs output to include Emby quality-run dev command.'
    }

    New-Item -ItemType Directory -Path $reportSetDir | Out-Null
    @'
{
  "runId": "netflix/chimera-4k-2398-hdr-pq",
  "metricVersion": "software-quality-v1",
  "source": {
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $reportSetDir 'case-a.json') -Encoding UTF8
    @'
{
  "runId": "jellyfin/dv-profile5-hevc-4k",
  "metricVersion": "software-quality-v1",
  "source": {
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "DolbyVisionUnsupported"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $reportSetDir 'case-b.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-report-set `
            --manifest $manifestPath `
            --reports-dir $reportSetDir `
            --output $reportSetValidationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI validate-report-set returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $reportSetValidation = Get-Content -Raw -LiteralPath $reportSetValidationPath | ConvertFrom-Json
    if ($reportSetValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI validate-report-set output to be valid.'
    }

    if ($reportSetValidation.matchedCaseCount -ne 2) {
        throw 'Expected playback quality CLI validate-report-set output to include two matched cases.'
    }

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

    New-Item -ItemType Directory -Path $runIdBaselineDir | Out-Null
    New-Item -ItemType Directory -Path $runIdCandidateDir | Out-Null
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
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
'@ | Set-Content -LiteralPath (Join-Path $runIdBaselineDir 'baseline-a.json') -Encoding UTF8
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
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
'@ | Set-Content -LiteralPath (Join-Path $runIdCandidateDir 'candidate-renamed.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- compare-suite `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $runIdCandidateDir `
            --match-by run-id `
            --comparisons-dir $runIdComparisonsDir `
            --output $runIdSuitePath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI compare-suite run-id matching returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $runIdSuite = Get-Content -Raw -LiteralPath $runIdSuitePath | ConvertFrom-Json
    if ($runIdSuite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI compare-suite run-id matching action to accept candidate.'
    }

    if (-not ($runIdSuite.cases | Where-Object { $_.caseId -eq 'item-1/source-1' -and $_.action -eq 'accept-candidate' })) {
        throw 'Expected playback quality CLI compare-suite run-id matching to use run-id case summary.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "item-1/source-1",
      "uri": "https://example.invalid/item-1/source-1.mp4",
      "tier": 2,
      "purpose": [
        "frame-pacing"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 23.976,
        "hdrKind": "Sdr"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $candidateEvaluationManifestPath -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $runIdCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationComparisonsDir `
            --output $candidateEvaluationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'playback quality CLI evaluate-candidate returned a non-zero exit code.'
        }
    }
    finally {
        Pop-Location
    }

    $candidateEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationPath | ConvertFrom-Json
    if ($candidateEvaluation.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI evaluate-candidate action to accept candidate.'
    }

    if ($candidateEvaluation.manifestValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI evaluate-candidate manifest validation to be valid.'
    }

    if ($candidateEvaluation.baselineReportSetValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI evaluate-candidate baseline report set validation to be valid.'
    }

    if ($candidateEvaluation.candidateReportSetValidation.isValid -ne $true) {
        throw 'Expected playback quality CLI evaluate-candidate candidate report set validation to be valid.'
    }

    if ($candidateEvaluation.suite.action -ne 'accept-candidate') {
        throw 'Expected playback quality CLI evaluate-candidate suite action to accept candidate.'
    }

    if ($null -eq $candidateEvaluation.activeGate -or
        $candidateEvaluation.activeGate.name -ne 'suite' -or
        $candidateEvaluation.activeGate.status -ne 'pass' -or
        $candidateEvaluation.activeGate.action -ne 'accept-candidate') {
        throw 'Expected evaluate-candidate active gate to point at passing suite decision.'
    }

    if ($null -eq $candidateEvaluation.baselineReportAnalysis -or
        $candidateEvaluation.baselineReportAnalysis.totalReportCount -ne 1 -or
        $candidateEvaluation.baselineReportAnalysis.analyzedReportCount -ne 0 -or
        $candidateEvaluation.baselineReportAnalysis.unavailableReportCount -ne 1 -or
        $candidateEvaluation.baselineReportAnalysis.blockedReportCount -ne 0) {
        throw 'Expected evaluate-candidate to summarize unavailable baseline report analysis for raw reports.'
    }

    if ($null -eq $candidateEvaluation.candidateReportAnalysis -or
        $candidateEvaluation.candidateReportAnalysis.totalReportCount -ne 1 -or
        $candidateEvaluation.candidateReportAnalysis.analyzedReportCount -ne 0 -or
        $candidateEvaluation.candidateReportAnalysis.unavailableReportCount -ne 1 -or
        $candidateEvaluation.candidateReportAnalysis.blockedReportCount -ne 0) {
        throw 'Expected evaluate-candidate to summarize unavailable candidate report analysis for raw reports.'
    }

    if (-not ($candidateEvaluation.candidateReportAnalysis.cases |
        Where-Object { $_.caseId -eq 'item-1/source-1' -and $_.status -eq 'unavailable' })) {
        throw 'Expected evaluate-candidate candidate report-analysis summary to include unavailable raw-report case.'
    }

    if ($null -eq $candidateEvaluation.evidenceGates -or $candidateEvaluation.evidenceGates.Count -ne 6) {
        throw 'Expected playback quality CLI evaluate-candidate to emit six evidence gates.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'manifest' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate manifest evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'baseline-report-set' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate baseline report-set evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'candidate-report-set' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate candidate report-set evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'baseline-report-analysis' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate baseline report-analysis evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'candidate-report-analysis' -and $_.status -eq 'pass' })) {
        throw 'Expected evaluate-candidate candidate report-analysis evidence gate to pass.'
    }

    if (-not ($candidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'pass' -and $_.action -eq 'accept-candidate' })) {
        throw 'Expected evaluate-candidate suite evidence gate to pass with accept-candidate action.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationInvalidCandidateDir | Out-Null
    @'
{
  "runId": "item-1/source-1",
  "metricVersion": "software-quality-v1",
  "source": {
    "itemId": "item-1",
    "mediaSourceId": "source-1",
    "codec": "hevc",
    "width": 3840,
    "height": 2160,
    "frameRate": 23.976,
    "hdrKind": "Hdr10"
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
'@ | Set-Content -LiteralPath (Join-Path $candidateEvaluationInvalidCandidateDir 'candidate-wrong-source.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $candidateEvaluationInvalidCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationInvalidComparisonsDir `
            --output $candidateEvaluationInvalidPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject invalid candidate evidence.'
        }
    }
    finally {
        Pop-Location
    }

    $invalidCandidateEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationInvalidPath | ConvertFrom-Json
    if ($invalidCandidateEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected invalid evaluate-candidate output to collect comparable evidence.'
    }

    if ($invalidCandidateEvaluation.candidateReportSetValidation.isValid -ne $false) {
        throw 'Expected invalid evaluate-candidate candidate report set validation to fail.'
    }

    if (-not ($invalidCandidateEvaluation.blockers -contains 'candidate-report-set.invalid')) {
        throw 'Expected invalid evaluate-candidate output to include candidate report set blocker.'
    }

    if ($null -eq $invalidCandidateEvaluation.activeGate -or
        $invalidCandidateEvaluation.activeGate.name -ne 'candidate-report-set' -or
        $invalidCandidateEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected invalid evaluate-candidate active gate to point at blocked candidate report-set gate.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationInvalidComparisonsDir) {
        throw 'Expected invalid evaluate-candidate evidence to skip comparison output.'
    }

    $invalidCandidateGate = $invalidCandidateEvaluation.evidenceGates |
        Where-Object { $_.name -eq 'candidate-report-set' } |
        Select-Object -First 1
    if ($null -eq $invalidCandidateGate -or $invalidCandidateGate.status -ne 'blocked') {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to be blocked.'
    }

    if (-not ($invalidCandidateGate.blockers -contains 'candidate-report-set.invalid')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include blocker.'
    }

    if (-not ($invalidCandidateGate.signals -contains 'source.hdrKind')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include mismatched source signal.'
    }

    if (-not ($invalidCandidateGate.caseIds -contains 'item-1/source-1')) {
        throw 'Expected invalid evaluate-candidate candidate report-set gate to include affected case id.'
    }

    if (-not ($invalidCandidateEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'skipped' })) {
        throw 'Expected invalid evaluate-candidate suite evidence gate to be skipped.'
    }

    New-Item -ItemType Directory -Path $baselineEvaluationBlockedAnalysisBaselineDir | Out-Null
    @'
{
  "report": {
    "runId": "item-1/source-1",
    "metricVersion": "software-quality-v1",
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "width": 3840,
      "height": 2160,
      "frameRate": 23.976,
      "hdrKind": "Sdr"
    },
    "checks": [
      {
        "name": "RenderedVideoFrames",
        "signal": "timing.renderedVideoFrames",
        "status": "fail",
        "failureArea": "startup",
        "expected": "1",
        "actual": "0"
      }
    ]
  },
  "modelAnalysis": {
    "runId": "item-1/source-1",
    "result": "fail",
    "optimizationGate": {
      "status": "blocked",
      "canOptimizePlaybackCore": false,
      "blockers": [
        "sample.insufficient"
      ],
      "blockerSignals": [
        "sample.status"
      ],
      "targetFailureAreas": []
    }
  }
}
'@ | Set-Content -LiteralPath (Join-Path $baselineEvaluationBlockedAnalysisBaselineDir 'baseline-blocked-analysis.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $baselineEvaluationBlockedAnalysisBaselineDir `
            --candidate-dir $runIdCandidateDir `
            --match-by run-id `
            --comparisons-dir $baselineEvaluationBlockedAnalysisComparisonsDir `
            --output $baselineEvaluationBlockedAnalysisPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject blocked baseline report analysis.'
        }
    }
    finally {
        Pop-Location
    }

    $blockedBaselineAnalysisEvaluation = Get-Content -Raw -LiteralPath $baselineEvaluationBlockedAnalysisPath | ConvertFrom-Json
    if ($blockedBaselineAnalysisEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected blocked baseline report-analysis output to collect comparable evidence.'
    }

    if ($null -eq $blockedBaselineAnalysisEvaluation.activeGate -or
        $blockedBaselineAnalysisEvaluation.activeGate.name -ne 'baseline-report-analysis' -or
        $blockedBaselineAnalysisEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected blocked baseline report-analysis active gate to point at baseline report-analysis gate.'
    }

    if (-not ($blockedBaselineAnalysisEvaluation.blockers -contains 'baseline-report-analysis.blocked')) {
        throw 'Expected blocked baseline report-analysis output to include baseline report-analysis blocker.'
    }

    if ($null -eq $blockedBaselineAnalysisEvaluation.baselineReportAnalysis -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.totalReportCount -ne 1 -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.analyzedReportCount -ne 1 -or
        $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.blockedReportCount -ne 1) {
        throw 'Expected blocked baseline report-analysis output to summarize analyzed blocked baseline report.'
    }

    $blockedBaselineAnalysisCase = $blockedBaselineAnalysisEvaluation.baselineReportAnalysis.cases |
        Where-Object { $_.caseId -eq 'item-1/source-1' } |
        Select-Object -First 1
    if ($null -eq $blockedBaselineAnalysisCase -or $blockedBaselineAnalysisCase.status -ne 'blocked') {
        throw 'Expected blocked baseline report-analysis summary to include blocked baseline case.'
    }

    if (-not ($blockedBaselineAnalysisCase.blockers -contains 'sample.insufficient')) {
        throw 'Expected blocked baseline report-analysis summary case to include model analysis blocker.'
    }

    if (-not ($blockedBaselineAnalysisCase.signals -contains 'sample.status')) {
        throw 'Expected blocked baseline report-analysis summary case to include model analysis blocker signal.'
    }

    if (Test-Path -LiteralPath $baselineEvaluationBlockedAnalysisComparisonsDir) {
        throw 'Expected blocked baseline report-analysis evidence to skip comparison output.'
    }

    New-Item -ItemType Directory -Path $candidateEvaluationBlockedAnalysisCandidateDir | Out-Null
    @'
{
  "report": {
    "runId": "item-1/source-1",
    "metricVersion": "software-quality-v1",
    "source": {
      "itemId": "item-1",
      "mediaSourceId": "source-1",
      "codec": "hevc",
      "width": 3840,
      "height": 2160,
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
  },
  "modelAnalysis": {
    "runId": "item-1/source-1",
    "result": "fail",
    "optimizationGate": {
      "status": "blocked",
      "canOptimizePlaybackCore": false,
      "blockers": [
        "source.mismatch"
      ],
      "blockerSignals": [
        "source.hdrKind"
      ],
      "targetFailureAreas": []
    }
  }
}
'@ | Set-Content -LiteralPath (Join-Path $candidateEvaluationBlockedAnalysisCandidateDir 'candidate-blocked-analysis.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj `
            --no-build `
            -- evaluate-candidate `
            --manifest $candidateEvaluationManifestPath `
            --baseline-dir $runIdBaselineDir `
            --candidate-dir $candidateEvaluationBlockedAnalysisCandidateDir `
            --match-by run-id `
            --comparisons-dir $candidateEvaluationBlockedAnalysisComparisonsDir `
            --output $candidateEvaluationBlockedAnalysisPath
        if ($LASTEXITCODE -eq 0) {
            throw 'Expected playback quality CLI evaluate-candidate to reject blocked candidate report analysis.'
        }
    }
    finally {
        Pop-Location
    }

    $blockedAnalysisEvaluation = Get-Content -Raw -LiteralPath $candidateEvaluationBlockedAnalysisPath | ConvertFrom-Json
    if ($blockedAnalysisEvaluation.action -ne 'collect-comparable-evidence') {
        throw 'Expected blocked report-analysis evaluate-candidate output to collect comparable evidence.'
    }

    if (-not ($blockedAnalysisEvaluation.reasons -contains 'candidate evaluation has blocked evidence gates')) {
        throw 'Expected blocked report-analysis evaluate-candidate output to explain blocked evidence gates.'
    }

    if ($blockedAnalysisEvaluation.reasons -contains 'candidate evaluation has invalid manifest or report-set evidence') {
        throw 'Expected blocked report-analysis evaluate-candidate output not to blame manifest or report-set evidence.'
    }

    if (-not ($blockedAnalysisEvaluation.blockers -contains 'candidate-report-analysis.blocked')) {
        throw 'Expected blocked report-analysis evaluate-candidate output to include candidate report-analysis blocker.'
    }

    if ($null -eq $blockedAnalysisEvaluation.activeGate -or
        $blockedAnalysisEvaluation.activeGate.name -ne 'candidate-report-analysis' -or
        $blockedAnalysisEvaluation.activeGate.status -ne 'blocked') {
        throw 'Expected blocked report-analysis active gate to point at blocked candidate report-analysis gate.'
    }

    if ($null -eq $blockedAnalysisEvaluation.candidateReportAnalysis -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.totalReportCount -ne 1 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.analyzedReportCount -ne 1 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.unavailableReportCount -ne 0 -or
        $blockedAnalysisEvaluation.candidateReportAnalysis.blockedReportCount -ne 1) {
        throw 'Expected blocked report-analysis evaluate-candidate output to summarize analyzed blocked candidate report.'
    }

    $blockedAnalysisCase = $blockedAnalysisEvaluation.candidateReportAnalysis.cases |
        Where-Object { $_.caseId -eq 'item-1/source-1' } |
        Select-Object -First 1
    if ($null -eq $blockedAnalysisCase -or $blockedAnalysisCase.status -ne 'blocked') {
        throw 'Expected blocked report-analysis summary to include blocked candidate case.'
    }

    if (-not ($blockedAnalysisCase.blockers -contains 'source.mismatch')) {
        throw 'Expected blocked report-analysis summary case to include model analysis blocker.'
    }

    if (-not ($blockedAnalysisCase.signals -contains 'source.hdrKind')) {
        throw 'Expected blocked report-analysis summary case to include model analysis blocker signal.'
    }

    if (Test-Path -LiteralPath $candidateEvaluationBlockedAnalysisComparisonsDir) {
        throw 'Expected blocked report-analysis evidence to skip comparison output.'
    }

    $blockedAnalysisGate = $blockedAnalysisEvaluation.evidenceGates |
        Where-Object { $_.name -eq 'candidate-report-analysis' } |
        Select-Object -First 1
    if ($null -eq $blockedAnalysisGate -or $blockedAnalysisGate.status -ne 'blocked') {
        throw 'Expected candidate report-analysis gate to be blocked.'
    }

    if (-not ($blockedAnalysisGate.blockers -contains 'source.mismatch')) {
        throw 'Expected candidate report-analysis gate to include model analysis blocker.'
    }

    if (-not ($blockedAnalysisGate.signals -contains 'source.hdrKind')) {
        throw 'Expected candidate report-analysis gate to include model analysis blocker signal.'
    }

    if (-not ($blockedAnalysisGate.caseIds -contains 'item-1/source-1')) {
        throw 'Expected candidate report-analysis gate to include affected case id.'
    }

    if (-not ($blockedAnalysisEvaluation.evidenceGates | Where-Object { $_.name -eq 'suite' -and $_.status -eq 'skipped' })) {
        throw 'Expected blocked report-analysis evaluate-candidate suite evidence gate to be skipped.'
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

    if (-not ($stallSuite.failureAreas -contains 'frame-pacing')) {
        throw 'Expected playback quality CLI compare-suite stall suite to include persisting failure area.'
    }

    if (-not ($stallSuite.targetFailureAreas -contains 'frame-pacing')) {
        throw 'Expected playback quality CLI compare-suite stall suite to include target failure area.'
    }

    if (-not ($stallSuite.signals -contains 'timing.maxFrameGapMs')) {
        throw 'Expected playback quality CLI compare-suite stall suite to include persisting failure signal.'
    }

    $stallCase = $stallSuite.cases | Select-Object -First 1
    if ($null -eq $stallCase -or -not ($stallCase.failureAreas -contains 'frame-pacing')) {
        throw 'Expected playback quality CLI compare-suite stall case to include persisting failure area.'
    }

    if (-not ($stallCase.signals -contains 'timing.maxFrameGapMs')) {
        throw 'Expected playback quality CLI compare-suite stall case to include persisting failure signal.'
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
