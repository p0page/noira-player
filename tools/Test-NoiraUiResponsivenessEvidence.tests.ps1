$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scriptPath = Join-Path $repoRoot 'tools\Test-NoiraUiResponsivenessEvidence.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('noira-ui-responsiveness-' + [guid]::NewGuid().ToString('N'))

function Write-EvidenceFixture(
    [string]$Directory,
    [bool]$Healthy = $true,
    [bool]$IncludeResume = $true
) {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $sessionStart = [DateTimeOffset]::Parse('2026-07-11T12:00:00+08:00')
    $watchdogPath = Join-Path $Directory 'ui-responsiveness.log'
    $watchdogLines = @($sessionStart.ToString('O') + ' session-start')
    foreach ($index in 1..26) {
        $timestamp = $sessionStart.AddSeconds($index * 5)
        $isHealthy = $Healthy -or $index -ne 10
        $maxDispatch = if ($isHealthy) { '12.5' } else { '1500.0' }
        $watchdogLines += (
            $timestamp.ToString('O') +
            ' completed=20 skipped=0 maxDispatchMs=' + $maxDispatch +
            ' pendingMs=0.0 healthy=' + $isHealthy
        )
    }

    Set-Content -LiteralPath $watchdogPath -Value $watchdogLines -Encoding UTF8

    $events = @(
        [ordered]@{ operation = 'load'; status = 'success' },
        [ordered]@{ operation = 'play'; status = 'success' },
        [ordered]@{ operation = 'pause'; status = 'success' }
    )
    if ($IncludeResume) {
        $events += [ordered]@{ operation = 'resume'; status = 'success' }
    }
    $events += [ordered]@{ operation = 'stop'; status = 'success' }

    $reportPath = Join-Path $Directory 'quality-report.json'
    $report = [ordered]@{
        report = [ordered]@{
            result = 'pass'
            lifecycle = [ordered]@{ events = $events }
            error = [ordered]@{ isTerminal = $false }
            runtimeMetrics = [ordered]@{
                status = 'captured'
                hasPlaybackSample = $true
            }
        }
    }
    Set-Content -LiteralPath $reportPath -Value ($report | ConvertTo-Json -Depth 8) -Encoding UTF8
    [System.IO.File]::SetLastWriteTimeUtc($reportPath, $sessionStart.AddSeconds(100).UtcDateTime)

    return [pscustomobject]@{
        WatchdogPath = $watchdogPath
        ReportPath = $reportPath
    }
}

function Invoke-EvidenceCheck([object]$Fixture) {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
        -WatchdogLogPath $Fixture.WatchdogPath `
        -QualityReportPath $Fixture.ReportPath `
        -MinimumHealthySeconds 120 `
        -MinimumPostReportHealthySeconds 20 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output | Out-String)
    }

    return $output
}

try {
    $passing = Write-EvidenceFixture (Join-Path $testRoot 'passing')
    $summary = Invoke-EvidenceCheck $passing | ConvertFrom-Json
    if ($summary.result -ne 'pass' -or $summary.healthyWindowCount -ne 26) {
        throw 'Expected healthy evidence to pass with all watchdog windows counted.'
    }

    $unhealthy = Write-EvidenceFixture (Join-Path $testRoot 'unhealthy') -Healthy $false
    $unhealthyFailed = $false
    try {
        Invoke-EvidenceCheck $unhealthy | Out-Null
    }
    catch {
        $unhealthyFailed = $_.Exception.Message -match 'unhealthy watchdog window'
    }
    if (-not $unhealthyFailed) {
        throw 'Expected an unhealthy watchdog window to fail the evidence check.'
    }

    $missingResume = Write-EvidenceFixture (Join-Path $testRoot 'missing-resume') -IncludeResume $false
    $lifecycleFailed = $false
    try {
        Invoke-EvidenceCheck $missingResume | Out-Null
    }
    catch {
        $lifecycleFailed = $_.Exception.Message -match 'resume'
    }
    if (-not $lifecycleFailed) {
        throw 'Expected a missing resume lifecycle operation to fail the evidence check.'
    }

    Write-Output 'ui responsiveness evidence tests ok'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
