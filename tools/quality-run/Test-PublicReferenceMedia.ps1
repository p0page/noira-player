param(
    [string]$ManifestPath = '',
    [string]$OutputPath = '',
    [string]$FfprobePath = 'ffprobe',
    [Alias('CaseId')]
    [string[]]$CaseIds = @()
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot 'docs\qa\playback-quality-reference-manifest.example.json'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot 'docs\qa\private\public-reference-media-probe.local.json'
}

function Normalize-String([object]$Value) {
    if ($null -eq $Value) {
        return ''
    }

    return ([string]$Value).Trim()
}

function Get-PropertyValue(
    [object]$Value,
    [string]$Name
) {
    if ($null -eq $Value) {
        return $null
    }

    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Test-PublicUri([string]$Uri) {
    $normalized = Normalize-String $Uri
    $normalized.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith('http://', [StringComparison]::OrdinalIgnoreCase)
}

function ConvertTo-FrameRate([object]$Value) {
    $text = Normalize-String $Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    if ($text.Contains('/')) {
        $parts = $text.Split('/')
        if ($parts.Length -ne 2) {
            return $null
        }

        $numerator = 0.0
        $denominator = 0.0
        if ([double]::TryParse($parts[0], [System.Globalization.NumberStyles]::Float, $culture, [ref]$numerator) -and
            [double]::TryParse($parts[1], [System.Globalization.NumberStyles]::Float, $culture, [ref]$denominator) -and
            $denominator -ne 0) {
            return $numerator / $denominator
        }

        return $null
    }

    $parsed = 0.0
    if ([double]::TryParse($text, [System.Globalization.NumberStyles]::Float, $culture, [ref]$parsed)) {
        return $parsed
    }

    return $null
}

function Get-ProbedHdrKind([object]$Stream) {
    $transfer = (Normalize-String (Get-PropertyValue $Stream 'color_transfer')).ToLowerInvariant()
    $primaries = (Normalize-String (Get-PropertyValue $Stream 'color_primaries')).ToLowerInvariant()
    $pixelFormat = (Normalize-String (Get-PropertyValue $Stream 'pix_fmt')).ToLowerInvariant()

    if ($transfer -eq 'smpte2084' -and $primaries -eq 'bt2020' -and $pixelFormat.Contains('10')) {
        return 'Hdr10'
    }

    if ($transfer -eq 'bt709' -or
        $transfer -eq 'iec61966-2-1' -or
        $primaries -eq 'bt709') {
        return 'Sdr'
    }

    return 'Unknown'
}

function New-Check(
    [string]$Signal,
    [object]$Expected,
    [object]$Actual,
    [string]$Status,
    [string]$Reason = ''
) {
    $check = [ordered]@{
        signal = $Signal
        expected = $Expected
        actual = $Actual
        status = $Status
    }

    if (-not [string]::IsNullOrWhiteSpace($Reason)) {
        $check.reason = $Reason
    }

    [pscustomobject]$check
}

function New-EqualityCheck(
    [string]$Signal,
    [object]$Expected,
    [object]$Actual
) {
    $expectedText = Normalize-String $Expected
    $actualText = Normalize-String $Actual
    $status = if ([string]::Equals($expectedText, $actualText, [StringComparison]::OrdinalIgnoreCase)) {
        'pass'
    } else {
        'fail'
    }

    New-Check -Signal $Signal -Expected $Expected -Actual $Actual -Status $status
}

function New-NumericCheck(
    [string]$Signal,
    [object]$Expected,
    [object]$Actual,
    [double]$Tolerance
) {
    if ($null -eq $Expected -or $null -eq $Actual) {
        return New-Check -Signal $Signal -Expected $Expected -Actual $Actual -Status 'fail'
    }

    $difference = [math]::Abs([double]$Expected - [double]$Actual)
    $status = if ($difference -le $Tolerance) { 'pass' } else { 'fail' }
    New-Check -Signal $Signal -Expected $Expected -Actual $Actual -Status $status
}

function Invoke-FfprobeJson(
    [string]$Uri,
    [string]$ProbePath
) {
    $probeArgs = @(
        '-v',
        'error',
        '-select_streams',
        'v:0',
        '-show_entries',
        'stream=codec_name,profile,width,height,pix_fmt,color_space,color_transfer,color_primaries,avg_frame_rate,r_frame_rate:format=duration,bit_rate',
        '-of',
        'json',
        $Uri
    )
    $global:LASTEXITCODE = 0
    $output = & $ProbePath @probeArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        return [pscustomobject]@{
            status = 'probe-failed'
            error = ($output -join "`n")
            payload = $null
        }
    }

    return [pscustomobject]@{
        status = 'pass'
        error = ''
        payload = (($output -join "`n") | ConvertFrom-Json)
    }
}

function Test-ExpectedHdrKind(
    [object]$Expected,
    [string]$Actual
) {
    $expectedText = Normalize-String $Expected
    if ([string]::IsNullOrWhiteSpace($expectedText)) {
        return $null
    }

    if ($expectedText -ne 'Hdr10' -and $expectedText -ne 'Sdr') {
        return New-Check `
            -Signal 'source.hdrKind' `
            -Expected $Expected `
            -Actual $Actual `
            -Status 'skipped' `
            -Reason 'hdr-kind-requires-player-or-dolby-vision-metadata'
    }

    New-EqualityCheck -Signal 'source.hdrKind' -Expected $Expected -Actual $Actual
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
$cases = @($manifest.cases)
if ($CaseIds.Count -gt 0) {
    $caseIdSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($id in $CaseIds) {
        [void]$caseIdSet.Add($id)
    }

    $cases = @($cases | Where-Object {
        $caseIdSet.Contains((Normalize-String (Get-PropertyValue $_ 'caseId')))
    })
}

$results = @()
foreach ($case in $cases) {
    $currentCaseId = Normalize-String (Get-PropertyValue $case 'caseId')
    $uri = Normalize-String (Get-PropertyValue $case 'uri')
    if (-not (Test-PublicUri $uri)) {
        $results += [pscustomobject]@{
            caseId = $currentCaseId
            uri = $uri
            status = 'skipped'
            reason = 'non-public-uri'
            checks = @()
        }
        continue
    }

    $probe = Invoke-FfprobeJson -Uri $uri -ProbePath $FfprobePath
    if ($probe.status -ne 'pass') {
        $results += [pscustomobject]@{
            caseId = $currentCaseId
            uri = $uri
            status = 'fail'
            reason = 'ffprobe-failed'
            error = $probe.error
            checks = @()
        }
        continue
    }

    $stream = @($probe.payload.streams) | Select-Object -First 1
    $format = $probe.payload.format
    $actualFrameRate = ConvertTo-FrameRate (Get-PropertyValue $stream 'avg_frame_rate')
    if ($null -eq $actualFrameRate) {
        $actualFrameRate = ConvertTo-FrameRate (Get-PropertyValue $stream 'r_frame_rate')
    }

    $expected = Get-PropertyValue $case 'expected'
    $checks = @()
    $checks += New-EqualityCheck `
        -Signal 'source.codec' `
        -Expected (Get-PropertyValue $expected 'codec') `
        -Actual (Get-PropertyValue $stream 'codec_name')
    $checks += New-NumericCheck `
        -Signal 'source.width' `
        -Expected (Get-PropertyValue $expected 'width') `
        -Actual (Get-PropertyValue $stream 'width') `
        -Tolerance 0
    $checks += New-NumericCheck `
        -Signal 'source.height' `
        -Expected (Get-PropertyValue $expected 'height') `
        -Actual (Get-PropertyValue $stream 'height') `
        -Tolerance 0
    $checks += New-NumericCheck `
        -Signal 'source.frameRate' `
        -Expected (Get-PropertyValue $expected 'frameRate') `
        -Actual $actualFrameRate `
        -Tolerance 0.01

    $hdrCheck = Test-ExpectedHdrKind `
        -Expected (Get-PropertyValue $expected 'hdrKind') `
        -Actual (Get-ProbedHdrKind $stream)
    if ($null -ne $hdrCheck) {
        $checks += $hdrCheck
    }

    $status = if ($checks | Where-Object { $_.status -eq 'fail' }) { 'fail' } else { 'pass' }
    $results += [pscustomobject]@{
        caseId = $currentCaseId
        uri = $uri
        status = $status
        checks = $checks
        probe = [pscustomobject]@{
            codec = Get-PropertyValue $stream 'codec_name'
            profile = Get-PropertyValue $stream 'profile'
            width = Get-PropertyValue $stream 'width'
            height = Get-PropertyValue $stream 'height'
            pixelFormat = Get-PropertyValue $stream 'pix_fmt'
            colorSpace = Get-PropertyValue $stream 'color_space'
            colorTransfer = Get-PropertyValue $stream 'color_transfer'
            colorPrimaries = Get-PropertyValue $stream 'color_primaries'
            frameRate = $actualFrameRate
            durationSeconds = Get-PropertyValue $format 'duration'
            bitRate = Get-PropertyValue $format 'bit_rate'
        }
    }
}

$report = [pscustomobject]@{
    schemaVersion = 1
    generatedAt = [DateTimeOffset]::UtcNow.ToString('O')
    manifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
    ffprobePath = $FfprobePath
    result = if ($results | Where-Object { $_.status -eq 'fail' }) { 'fail' } else { 'pass' }
    caseCount = $cases.Count
    probedCaseCount = @($results | Where-Object { $_.status -ne 'skipped' }).Count
    cases = $results
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output ('wrote public reference media probe report: ' + $OutputPath)
Write-Output ('result: ' + $report.result)

if ($report.result -ne 'pass') {
    exit 1
}
