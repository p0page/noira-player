$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$scriptPath = Join-Path $repoRoot 'tools\quality-run\New-PlaybackCoreTuningBaseline.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('playback-core-tuning-baseline-test-' + [Guid]::NewGuid().ToString('N'))

try {
    powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
        -NoPrivateManifest `
        -SkipNativeHeadless `
        -OutputRoot $tempRoot
    if ($LASTEXITCODE -ne 0) {
        throw 'New-PlaybackCoreTuningBaseline.ps1 returned a non-zero exit code.'
    }

    $summaryPath = Join-Path $tempRoot 'baseline-summary.local.json'
    $manifestPath = Join-Path $tempRoot 'manifests\unified-reference-manifest.local.json'
    $validationPath = Join-Path $tempRoot 'summaries\report-set-validation.local.json'
    $analysisPath = Join-Path $tempRoot 'summaries\report-analysis-summary.local.json'
    $runPlanPath = Join-Path $tempRoot 'summaries\run-plan.local.json'
    $reportsDir = Join-Path $tempRoot 'reports'

    foreach ($path in @($summaryPath, $manifestPath, $validationPath, $analysisPath, $runPlanPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw ('Expected baseline artifact was not written: ' + $path)
        }
    }

    if (-not (Test-Path -LiteralPath $reportsDir)) {
        throw 'Expected baseline reports directory to exist.'
    }

    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    if ($summary.kind -ne 'playback-core-tuning-baseline') {
        throw 'Expected baseline summary kind.'
    }

    if ($summary.nativeHeadless.included -ne $false) {
        throw 'Expected test baseline to skip native-headless samples.'
    }

    if ($summary.validation.isValid -ne $true) {
        throw 'Expected test baseline report-set validation to be valid.'
    }

    if ($summary.analysis.totalReportCount -lt 1) {
        throw 'Expected test baseline to contain reports.'
    }

    if (-not ($summary.warnings -contains 'native-headless local generated samples were skipped by -SkipNativeHeadless')) {
        throw 'Expected summary to record the skipped native-headless warning.'
    }

    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or @($manifest.cases).Count -lt 1) {
        throw 'Expected unified manifest to contain reference cases.'
    }

    $validation = Get-Content -Raw -LiteralPath $validationPath | ConvertFrom-Json
    if ($validation.isValid -ne $true -or $validation.matchedCaseCount -ne @($manifest.cases).Count) {
        throw 'Expected all test baseline manifest cases to have matching reports.'
    }

    Write-Output 'playback-core-tuning-baseline tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
