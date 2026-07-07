$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Export-AppQualityRunReports.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('export-app-quality-run-test-' + [guid]::NewGuid().ToString('N'))
$packagesRoot = Join-Path $testRoot 'Packages'
$packageRoot = Join-Path $packagesRoot 'NextGenEmby.App_testpublisher'
$capturedRoot = Join-Path $packageRoot 'LocalState\quality-run\captured'
$outputRoot = Join-Path $testRoot 'exported'

New-Item -ItemType Directory -Path (Join-Path $capturedRoot 'local') -Force | Out-Null
@'
{
  "schemaVersion": 1,
  "caseMetadata": {
    "caseId": "local/sdr-smoke"
  },
  "report": {
    "runId": "local/sdr-smoke"
  }
}
'@ | Set-Content -LiteralPath (Join-Path $capturedRoot 'local\sdr-smoke.json') -Encoding UTF8

try {
    $summaryPath = Join-Path $testRoot 'summary.json'
    & $scriptPath `
        -PackagesRoot $packagesRoot `
        -OutputDirectory $outputRoot `
        -SummaryPath $summaryPath

    $exportedReport = Join-Path $outputRoot 'local\sdr-smoke.json'
    if (-not (Test-Path -LiteralPath $exportedReport)) {
        throw 'Expected captured quality-run report to be exported with relative path preserved.'
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($summary.packageRoot -ne $packageRoot) {
        throw 'Expected summary to record selected package root.'
    }

    if ($summary.exportedReportCount -ne 1) {
        throw 'Expected summary to count exported reports.'
    }

    if ($summary.reports[0].relativePath -ne 'local/sdr-smoke.json') {
        throw 'Expected summary to normalize relative report path.'
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output 'export-app-quality-run reports tests ok'
