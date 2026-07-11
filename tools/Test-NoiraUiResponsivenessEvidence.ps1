param(
    [Parameter(Mandatory = $true)]
    [string]$WatchdogLogPath,

    [Parameter(Mandatory = $true)]
    [string]$QualityReportPath,

    [ValidateRange(5, 3600)]
    [int]$MinimumHealthySeconds = 120,

    [ValidateRange(0, 600)]
    [int]$MinimumPostReportHealthySeconds = 10,

    [ValidateRange(1, 10000)]
    [double]$MaximumDispatchLatencyMs = 1000
)

$ErrorActionPreference = 'Stop'
$culture = [System.Globalization.CultureInfo]::InvariantCulture

function Resolve-ExistingFile([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name was not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Parse-Number([string]$Value, [string]$Name) {
    $parsed = 0.0
    if (-not [double]::TryParse(
        $Value,
        [System.Globalization.NumberStyles]::Float,
        $culture,
        [ref]$parsed)) {
        throw "Invalid $Name value in watchdog log: $Value"
    }

    return $parsed
}

$WatchdogLogPath = Resolve-ExistingFile $WatchdogLogPath 'Watchdog log'
$QualityReportPath = Resolve-ExistingFile $QualityReportPath 'Quality report'
$lines = @(Get-Content -LiteralPath $WatchdogLogPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$sessionStartIndex = -1
for ($index = $lines.Count - 1; $index -ge 0; $index--) {
    if ($lines[$index] -match '^(?<timestamp>\S+)\s+session-start$') {
        $sessionStartIndex = $index
        break
    }
}

if ($sessionStartIndex -lt 0) {
    throw 'Watchdog log does not contain a session-start marker.'
}

$sessionMatch = [regex]::Match($lines[$sessionStartIndex], '^(?<timestamp>\S+)\s+session-start$')
$sessionStart = [DateTimeOffset]::Parse(
    $sessionMatch.Groups['timestamp'].Value,
    $culture,
    [System.Globalization.DateTimeStyles]::RoundtripKind)
$windowPattern = [regex]::new(
    '^(?<timestamp>\S+)\s+completed=(?<completed>\d+)\s+skipped=(?<skipped>\d+)\s+' +
    'maxDispatchMs=(?<max>[0-9.]+)\s+pendingMs=(?<pending>[0-9.]+)\s+healthy=(?<healthy>True|False)$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$windows = @()
foreach ($line in $lines[($sessionStartIndex + 1)..($lines.Count - 1)]) {
    $match = $windowPattern.Match($line)
    if (-not $match.Success) {
        throw "Unrecognized watchdog line: $line"
    }

    $windows += [pscustomobject]@{
        Timestamp = [DateTimeOffset]::Parse(
            $match.Groups['timestamp'].Value,
            $culture,
            [System.Globalization.DateTimeStyles]::RoundtripKind)
        Completed = [int]$match.Groups['completed'].Value
        Skipped = [int]$match.Groups['skipped'].Value
        MaximumDispatchMs = Parse-Number $match.Groups['max'].Value 'maxDispatchMs'
        PendingMs = Parse-Number $match.Groups['pending'].Value 'pendingMs'
        Healthy = [bool]::Parse($match.Groups['healthy'].Value)
    }
}

if ($windows.Count -eq 0) {
    throw 'Watchdog log does not contain any completed observation windows.'
}

$unhealthy = @($windows | Where-Object {
    -not $_.Healthy -or
    $_.Completed -le 0 -or
    $_.MaximumDispatchMs -gt $MaximumDispatchLatencyMs -or
    $_.PendingMs -gt $MaximumDispatchLatencyMs
})
if ($unhealthy.Count -gt 0) {
    $first = $unhealthy[0]
    throw (
        'Found an unhealthy watchdog window at ' + $first.Timestamp.ToString('O') +
        ': completed=' + $first.Completed +
        ', maxDispatchMs=' + $first.MaximumDispatchMs.ToString('F1', $culture) +
        ', pendingMs=' + $first.PendingMs.ToString('F1', $culture) + '.'
    )
}

$lastWindow = $windows[-1]
$observedHealthySeconds = ($lastWindow.Timestamp - $sessionStart).TotalSeconds
if ($observedHealthySeconds -lt $MinimumHealthySeconds) {
    throw (
        'Healthy watchdog duration was too short: ' +
        $observedHealthySeconds.ToString('F1', $culture) +
        ' seconds; required ' + $MinimumHealthySeconds + ' seconds.'
    )
}

$qualityEnvelope = Get-Content -LiteralPath $QualityReportPath -Raw | ConvertFrom-Json
$qualityReport = if ($null -ne $qualityEnvelope.report) { $qualityEnvelope.report } else { $qualityEnvelope }
if ($qualityReport.result -ne 'pass') {
    throw "Playback-quality report result was not pass: $($qualityReport.result)"
}

if ($qualityReport.error.isTerminal -eq $true) {
    throw 'Playback-quality report contains a terminal playback error.'
}

if ($qualityReport.runtimeMetrics.status -ne 'captured' -or
    $qualityReport.runtimeMetrics.hasPlaybackSample -ne $true) {
    throw 'Playback-quality report does not contain a captured native playback sample.'
}

$requiredOperations = @('load', 'play', 'pause', 'resume', 'stop')
$events = @($qualityReport.lifecycle.events)
foreach ($operation in $requiredOperations) {
    $successful = @($events | Where-Object {
        $_.operation -eq $operation -and $_.status -eq 'success'
    }).Count -gt 0
    if (-not $successful) {
        throw "Playback-quality report is missing a successful $operation lifecycle operation."
    }
}

$reportWrittenAt = [DateTimeOffset](Get-Item -LiteralPath $QualityReportPath).LastWriteTimeUtc
$postReportHealthySeconds = ($lastWindow.Timestamp.ToUniversalTime() - $reportWrittenAt).TotalSeconds
if ($postReportHealthySeconds -lt $MinimumPostReportHealthySeconds) {
    throw (
        'Watchdog evidence after report creation was too short: ' +
        $postReportHealthySeconds.ToString('F1', $culture) +
        ' seconds; required ' + $MinimumPostReportHealthySeconds + ' seconds.'
    )
}

$summary = [ordered]@{
    schemaVersion = 1
    result = 'pass'
    sessionStartedAt = $sessionStart.ToString('O')
    lastHealthyWindowAt = $lastWindow.Timestamp.ToString('O')
    healthyWindowCount = $windows.Count
    observedHealthySeconds = [math]::Round($observedHealthySeconds, 1)
    postReportHealthySeconds = [math]::Round($postReportHealthySeconds, 1)
    maximumDispatchLatencyMs = [math]::Round(($windows | Measure-Object MaximumDispatchMs -Maximum).Maximum, 1)
    maximumPendingMs = [math]::Round(($windows | Measure-Object PendingMs -Maximum).Maximum, 1)
    skippedProbeCount = ($windows | Measure-Object Skipped -Sum).Sum
    requiredLifecycleOperations = $requiredOperations
}

$summary | ConvertTo-Json -Depth 4
