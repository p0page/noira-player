param(
    [Parameter(Mandatory = $true)]
    [string]$ReportsRoot,
    [string]$OutputPath = '',
    [int]$MinimumSamples = 3,
    [double]$MaterialityMs = 2.0,
    [string]$CaseGroupPattern = '^(?<group>.+?)(?:[-_]repeat[-_]\d+|[-_]run[-_]\d+)$'
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

function Normalize-String([object]$Value) {
    if ($null -eq $Value) {
        return ''
    }

    return ([string]$Value).Trim()
}

function Get-DoubleValue([object]$Value) {
    if ($null -eq $Value) {
        return $null
    }

    try {
        return [double]$Value
    }
    catch {
        return $null
    }
}

function Get-IntValue([object]$Value) {
    if ($null -eq $Value) {
        return 0
    }

    try {
        return [int]$Value
    }
    catch {
        return 0
    }
}

function Get-CaseGroupId([string]$CaseId, [string]$Pattern) {
    $normalized = Normalize-String $CaseId
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return ''
    }

    $match = [regex]::Match($normalized, $Pattern)
    if ($match.Success -and $match.Groups['group'].Success) {
        return $match.Groups['group'].Value
    }

    return $normalized
}

function Get-SpreadStats([double[]]$Values) {
    $usable = @($Values | Where-Object { $null -ne $_ })
    if ($usable.Count -eq 0) {
        return [pscustomobject][ordered]@{
            min = $null
            max = $null
            spread = $null
        }
    }

    $min = ($usable | Measure-Object -Minimum).Minimum
    $max = ($usable | Measure-Object -Maximum).Maximum
    [pscustomobject][ordered]@{
        min = [math]::Round([double]$min, 6)
        max = [math]::Round([double]$max, 6)
        spread = [math]::Round(([double]$max - [double]$min), 6)
    }
}

function New-SignalIfUnstable(
    [string]$Signal,
    [object]$Stats,
    [double]$Threshold
) {
    if ($null -ne $Stats.spread -and [double]$Stats.spread -ge $Threshold) {
        return $Signal
    }

    return $null
}

$resolvedReportsRoot = (Resolve-Path $ReportsRoot).Path
if (-not (Test-Path -LiteralPath $resolvedReportsRoot)) {
    throw ('ReportsRoot was not found: ' + $ReportsRoot)
}

if ($MinimumSamples -le 0) {
    throw 'MinimumSamples must be positive.'
}

if ($MaterialityMs -le 0) {
    throw 'MaterialityMs must be positive.'
}

$reportFiles = @(Get-ChildItem -LiteralPath $resolvedReportsRoot -Filter '*.json' -Recurse)
if ($reportFiles.Count -eq 0) {
    throw ('No JSON reports were found under ReportsRoot: ' + $resolvedReportsRoot)
}

$samples = @()
foreach ($file in $reportFiles) {
    $json = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
    $caseId = Normalize-String $json.caseMetadata.caseId
    if ([string]::IsNullOrWhiteSpace($caseId)) {
        $caseId = Normalize-String $json.report.runId
    }

    if ([string]::IsNullOrWhiteSpace($caseId)) {
        continue
    }

    $timing = $json.report.timing
    $sync = $json.report.sync
    if ($null -eq $timing) {
        continue
    }

    $expectedFrameDurationMs = Get-DoubleValue $timing.expectedFrameDurationMs
    $renderIntervalMsP95 = Get-DoubleValue $timing.renderIntervalMsP95
    $renderIntervalMsP99 = Get-DoubleValue $timing.renderIntervalMsP99
    $maxFrameGapMs = Get-DoubleValue $timing.maxFrameGapMs
    $audioAheadWaitOversleepP95 = Get-DoubleValue $timing.audioAheadWaitOversleepMsP95
    $audioAheadWaitOversleepP99 = Get-DoubleValue $timing.audioAheadWaitOversleepMsP99
    $audioVideoDriftP95 = Get-DoubleValue $sync.audioVideoDriftMsP95
    $audioVideoDriftP99 = Get-DoubleValue $sync.audioVideoDriftMsP99
    if ($null -eq $expectedFrameDurationMs -or
        $expectedFrameDurationMs -le 0 -or
        $null -eq $renderIntervalMsP95 -or
        $null -eq $renderIntervalMsP99 -or
        $null -eq $maxFrameGapMs) {
        continue
    }

    $samples += [pscustomobject][ordered]@{
        caseId = $caseId
        caseGroupId = Get-CaseGroupId -CaseId $caseId -Pattern $CaseGroupPattern
        path = $file.FullName
        result = Normalize-String $json.report.result
        renderedVideoFrames = Get-IntValue $timing.renderedVideoFrames
        expectedFrameDurationMs = [math]::Round($expectedFrameDurationMs, 6)
        renderIntervalMsP95 = [math]::Round($renderIntervalMsP95, 6)
        renderIntervalMsP99 = [math]::Round($renderIntervalMsP99, 6)
        maxFrameGapMs = [math]::Round($maxFrameGapMs, 6)
        renderIntervalP95ExpectedErrorMs = [math]::Round([math]::Abs($renderIntervalMsP95 - $expectedFrameDurationMs), 6)
        renderIntervalP99ExpectedErrorMs = [math]::Round([math]::Abs($renderIntervalMsP99 - $expectedFrameDurationMs), 6)
        maxFrameGapExpectedErrorMs = [math]::Round([math]::Abs($maxFrameGapMs - $expectedFrameDurationMs), 6)
        audioAheadWaitOversleepMsP95 = if ($null -eq $audioAheadWaitOversleepP95) { $null } else { [math]::Round($audioAheadWaitOversleepP95, 6) }
        audioAheadWaitOversleepMsP99 = if ($null -eq $audioAheadWaitOversleepP99) { $null } else { [math]::Round($audioAheadWaitOversleepP99, 6) }
        audioVideoDriftMsP95 = if ($null -eq $audioVideoDriftP95) { $null } else { [math]::Round($audioVideoDriftP95, 6) }
        audioVideoDriftMsP99 = if ($null -eq $audioVideoDriftP99) { $null } else { [math]::Round($audioVideoDriftP99, 6) }
    }
}

if ($samples.Count -eq 0) {
    throw 'No reports contained usable cadence timing evidence.'
}

$groups = @()
foreach ($group in ($samples | Group-Object -Property caseGroupId | Sort-Object Name)) {
    $groupSamples = @($group.Group | Sort-Object caseId)
    $p95Stats = Get-SpreadStats @($groupSamples | ForEach-Object { [double]$_.renderIntervalP95ExpectedErrorMs })
    $p99Stats = Get-SpreadStats @($groupSamples | ForEach-Object { [double]$_.renderIntervalP99ExpectedErrorMs })
    $maxGapStats = Get-SpreadStats @($groupSamples | ForEach-Object { [double]$_.maxFrameGapExpectedErrorMs })
    $audioAheadWaitOversleepP95Stats = Get-SpreadStats @($groupSamples | ForEach-Object { $_.audioAheadWaitOversleepMsP95 })
    $audioAheadWaitOversleepP99Stats = Get-SpreadStats @($groupSamples | ForEach-Object { $_.audioAheadWaitOversleepMsP99 })
    $audioVideoDriftP95Stats = Get-SpreadStats @($groupSamples | ForEach-Object { $_.audioVideoDriftMsP95 })
    $audioVideoDriftP99Stats = Get-SpreadStats @($groupSamples | ForEach-Object { $_.audioVideoDriftMsP99 })

    $unstableSignals = @(
        New-SignalIfUnstable -Signal 'framePacing.renderIntervalP95ExpectedErrorMs' -Stats $p95Stats -Threshold $MaterialityMs
        New-SignalIfUnstable -Signal 'framePacing.renderIntervalP99ExpectedErrorMs' -Stats $p99Stats -Threshold $MaterialityMs
        New-SignalIfUnstable -Signal 'framePacing.maxFrameGapExpectedErrorMs' -Stats $maxGapStats -Threshold $MaterialityMs
        New-SignalIfUnstable -Signal 'timing.audioAheadWaitOversleepMsP95' -Stats $audioAheadWaitOversleepP95Stats -Threshold $MaterialityMs
        New-SignalIfUnstable -Signal 'timing.audioAheadWaitOversleepMsP99' -Stats $audioAheadWaitOversleepP99Stats -Threshold $MaterialityMs
        New-SignalIfUnstable -Signal 'sync.audioVideoDriftMsP95' -Stats $audioVideoDriftP95Stats -Threshold $MaterialityMs
        New-SignalIfUnstable -Signal 'sync.audioVideoDriftMsP99' -Stats $audioVideoDriftP99Stats -Threshold $MaterialityMs
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $stability = 'stable'
    if ($groupSamples.Count -lt $MinimumSamples) {
        $stability = 'insufficient-samples'
    }
    elseif ($unstableSignals.Count -gt 0) {
        $stability = 'unstable'
    }

    $groups += [pscustomobject][ordered]@{
        caseGroupId = $group.Name
        stability = $stability
        sampleCount = $groupSamples.Count
        materialityMs = $MaterialityMs
        renderIntervalP95ExpectedErrorMinMs = $p95Stats.min
        renderIntervalP95ExpectedErrorMaxMs = $p95Stats.max
        renderIntervalP95ExpectedErrorSpreadMs = $p95Stats.spread
        renderIntervalP99ExpectedErrorMinMs = $p99Stats.min
        renderIntervalP99ExpectedErrorMaxMs = $p99Stats.max
        renderIntervalP99ExpectedErrorSpreadMs = $p99Stats.spread
        maxFrameGapExpectedErrorMinMs = $maxGapStats.min
        maxFrameGapExpectedErrorMaxMs = $maxGapStats.max
        maxFrameGapExpectedErrorSpreadMs = $maxGapStats.spread
        audioAheadWaitOversleepP95MinMs = $audioAheadWaitOversleepP95Stats.min
        audioAheadWaitOversleepP95MaxMs = $audioAheadWaitOversleepP95Stats.max
        audioAheadWaitOversleepP95SpreadMs = $audioAheadWaitOversleepP95Stats.spread
        audioAheadWaitOversleepP99MinMs = $audioAheadWaitOversleepP99Stats.min
        audioAheadWaitOversleepP99MaxMs = $audioAheadWaitOversleepP99Stats.max
        audioAheadWaitOversleepP99SpreadMs = $audioAheadWaitOversleepP99Stats.spread
        audioVideoDriftP95MinMs = $audioVideoDriftP95Stats.min
        audioVideoDriftP95MaxMs = $audioVideoDriftP95Stats.max
        audioVideoDriftP95SpreadMs = $audioVideoDriftP95Stats.spread
        audioVideoDriftP99MinMs = $audioVideoDriftP99Stats.min
        audioVideoDriftP99MaxMs = $audioVideoDriftP99Stats.max
        audioVideoDriftP99SpreadMs = $audioVideoDriftP99Stats.spread
        unstableSignals = @($unstableSignals)
        samples = @($groupSamples)
    }
}

$summary = [pscustomobject][ordered]@{
    schemaVersion = 1
    kind = 'playback-cadence-stability-summary'
    reportsRoot = $resolvedReportsRoot
    minimumSamples = $MinimumSamples
    materialityMs = $MaterialityMs
    totalReportCount = $reportFiles.Count
    usableSampleCount = $samples.Count
    totalGroupCount = $groups.Count
    stableGroupCount = @($groups | Where-Object { $_.stability -eq 'stable' }).Count
    unstableGroupCount = @($groups | Where-Object { $_.stability -eq 'unstable' }).Count
    insufficientSampleGroupCount = @($groups | Where-Object { $_.stability -eq 'insufficient-samples' }).Count
    unstableCaseGroupIds = @($groups | Where-Object { $_.stability -eq 'unstable' } | ForEach-Object { $_.caseGroupId })
    groups = @($groups)
}

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $outputParent = Split-Path $OutputPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($outputParent)) {
        New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
    }

    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    Write-Output ('wrote playback cadence stability summary: ' + $OutputPath)
}
else {
    $summary | ConvertTo-Json -Depth 12
}

$global:LASTEXITCODE = 0
