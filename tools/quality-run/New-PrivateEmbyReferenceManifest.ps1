param(
    [string]$ServerUrl = $env:NOIRAPLAYER_QA_SERVER_URL,
    [string]$UserName = $env:NOIRAPLAYER_QA_USERNAME,
    [string]$Password = $env:NOIRAPLAYER_QA_PASSWORD,
    [string]$ItemsJsonPath = '',
    [string]$PlaybackInfoJsonDirectory = '',
    [string]$OutputPath = '',
    [string]$SearchTerm = '',
    [int]$Limit = 300,
    [int]$MinBufferingBitrate = 40000000
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot 'docs\qa\private\emby-reference-manifest.local.json'
}

function Normalize-String([object]$Value) {
    if ($null -eq $Value) {
        return ''
    }

    return ([string]$Value).Trim()
}

function Normalize-ServerUrl([string]$Value) {
    (Normalize-String $Value).TrimEnd('/')
}

function Get-Array([object]$Value) {
    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Array]) {
        return @($Value)
    }

    return @($Value)
}

function Test-ContainsAny([string]$Value, [string[]]$Needles) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    foreach ($needle in $Needles) {
        if ($Value.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Get-NullableInt([object]$Value) {
    if ($null -eq $Value) {
        return $null
    }

    $text = Normalize-String $Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    $parsed = 0
    if ([int]::TryParse($text, [ref]$parsed)) {
        return $parsed
    }

    return $null
}

function Get-DolbyVisionProfile([string]$Combined) {
    $profileMatch = [regex]::Match(
        $Combined,
        '\b(?:profile|p)\s*(?<profile>5|7|8)(?:\.(?<compat>\d))?',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($profileMatch.Success) {
        return Get-NullableInt $profileMatch.Groups['profile'].Value
    }

    $codecMatch = [regex]::Match(
        $Combined,
        '\bdv(?:he|h1)\.0?(?<profile>5|7|8)',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($codecMatch.Success) {
        return Get-NullableInt $codecMatch.Groups['profile'].Value
    }

    return $null
}

function Get-DolbyVisionCompatibilityId([string]$Combined, [Nullable[int]]$Profile) {
    $profileMatch = [regex]::Match(
        $Combined,
        '\b(?:profile|p)\s*(?<profile>5|7|8)(?:\.(?<compat>\d))?',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($profileMatch.Success) {
        $compat = Get-NullableInt $profileMatch.Groups['compat'].Value
        if ($null -ne $compat) {
            return $compat
        }
    }

    if ($Combined -match '(?i)(?:compat|compatibility)\s*(?:id)?\s*[:=]?\s*1') {
        return 1
    }

    if ($Combined -match '(?i)(?:compat|compatibility)\s*(?:id)?\s*[:=]?\s*4') {
        return 4
    }

    if ($Profile -eq 8 -and (Test-ContainsAny $Combined @('hdr10'))) {
        return 1
    }

    if ($Profile -eq 8 -and (Test-ContainsAny $Combined @('hlg'))) {
        return 4
    }

    return $null
}

function Test-ContainsDolbyVisionHint([string]$Combined) {
    if ([string]::IsNullOrWhiteSpace($Combined)) {
        return $false
    }

    $Combined -match '(?i)dolby\s*vision|\bdovi\b|\bdv\b|\bdvhe\.0?[578]\b|\bdvh1\.0?[578]\b|\bdv\s*(?:p|profile)?\s*[578]\b'
}

function Test-HasNameOnlyDolbyVisionHint([object]$Item, [object]$MediaSource, [object]$VideoStream) {
    $combined = @(
        (Normalize-String $Item.Name),
        (Normalize-String $MediaSource.Name),
        (Normalize-String $MediaSource.DisplayTitle),
        (Normalize-String $VideoStream.Title),
        (Normalize-String $VideoStream.DisplayTitle)
    ) -join ' '

    Test-ContainsDolbyVisionHint $combined
}

function New-UnknownHdrProfileFromNameHint() {
    [pscustomobject][ordered]@{
        kind = 'UnknownHdr'
        strategy = 'Unknown HDR'
        isHdr = $true
        isDirectPlayable = $false
        isDolbyVision = $false
        dolbyVisionProfile = $null
        dolbyVisionCompatibilityId = $null
        hasHdr10BaseLayer = $false
        hasHlgBaseLayer = $false
    }
}

function Get-HdrProfile([object]$VideoStream) {
    $videoRange = Normalize-String $VideoStream.VideoRange
    $colorPrimaries = Normalize-String $VideoStream.ColorPrimaries
    $colorTransfer = Normalize-String $VideoStream.ColorTransfer
    $colorSpace = Normalize-String $VideoStream.ColorSpace
    $codec = Normalize-String $VideoStream.Codec

    # Deliberately exclude item names, source names, and stream display titles.
    $combined = @($videoRange, $colorPrimaries, $colorTransfer, $colorSpace, $codec) -join ' '
    $hasDolbyVision = Test-ContainsDolbyVisionHint $combined
    $hasHdr10 = (Test-ContainsAny $combined @('hdr10', 'hdr10+')) -or (
        (Test-ContainsAny $colorTransfer @('smpte2084', 'pq')) -and
        (Test-ContainsAny $colorPrimaries @('bt2020')) -and
        (Test-ContainsAny $colorSpace @('bt2020')))
    $hasHlg = Test-ContainsAny $combined @('hlg', 'arib-std-b67')
    $hasBt2020 = (Test-ContainsAny $colorPrimaries @('bt2020')) -and
        (Test-ContainsAny $colorSpace @('bt2020'))

    $profile = [ordered]@{
        kind = 'Sdr'
        strategy = 'SDR'
        isHdr = $false
        isDirectPlayable = $true
        isDolbyVision = $false
        dolbyVisionProfile = $null
        dolbyVisionCompatibilityId = $null
        hasHdr10BaseLayer = $false
        hasHlgBaseLayer = $false
    }

    if ($hasDolbyVision) {
        $profile.isHdr = $true
        $profile.isDolbyVision = $true
        $dvProfile = Get-DolbyVisionProfile $combined
        $compat = Get-DolbyVisionCompatibilityId $combined $dvProfile
        $profile.dolbyVisionProfile = $dvProfile
        $profile.dolbyVisionCompatibilityId = $compat

        if ($compat -eq 1) {
            $hasHdr10 = $true
        }

        if ($compat -eq 4) {
            $hasHlg = $true
        }

        if ($dvProfile -eq 5 -and -not $hasHdr10 -and -not $hasHlg) {
            $profile.kind = 'DolbyVisionUnsupported'
            $profile.strategy = 'Dolby Vision unsupported'
            $profile.isDirectPlayable = $false
            return [pscustomobject]$profile
        }

        if ($hasHdr10 -or $dvProfile -eq 7) {
            $profile.kind = 'DolbyVisionWithHdr10Fallback'
            $profile.strategy = 'HDR10 fallback from Dolby Vision'
            $profile.hasHdr10BaseLayer = $true
            return [pscustomobject]$profile
        }

        if ($hasHlg) {
            $profile.kind = 'DolbyVisionWithHlgFallback'
            $profile.strategy = 'HLG fallback from Dolby Vision'
            $profile.hasHlgBaseLayer = $true
            return [pscustomobject]$profile
        }

        $profile.kind = 'UnknownHdr'
        $profile.strategy = 'Unknown HDR'
        return [pscustomobject]$profile
    }

    if ($hasHlg) {
        $profile.kind = 'Hlg'
        $profile.strategy = 'HLG'
        $profile.isHdr = $true
        return [pscustomobject]$profile
    }

    if ($hasHdr10) {
        $profile.kind = 'Hdr10'
        $profile.strategy = 'HDR10'
        $profile.isHdr = $true
        return [pscustomobject]$profile
    }

    if ((Test-ContainsAny $combined @('hdr')) -or $hasBt2020) {
        $profile.kind = 'UnknownHdr'
        $profile.strategy = 'Unknown HDR'
        $profile.isHdr = $true
        return [pscustomobject]$profile
    }

    return [pscustomobject]$profile
}

function Get-VideoStream([object]$MediaSource) {
    foreach ($stream in (Get-Array $MediaSource.MediaStreams)) {
        if ((Normalize-String $stream.Type) -ieq 'Video') {
            return $stream
        }
    }

    return $null
}

function Get-FrameRate([object]$VideoStream) {
    $real = [double]$VideoStream.RealFrameRate
    if ($real -gt 0) {
        return $real
    }

    $average = [double]$VideoStream.AverageFrameRate
    if ($average -gt 0) {
        return $average
    }

    return 0.0
}

function Test-IsCadence23976([double]$FrameRate) {
    [math]::Abs($FrameRate - 23.976) -le 0.02 -or [math]::Abs($FrameRate - 23.98) -le 0.02
}

function New-Expected(
    [object]$VideoStream,
    [double]$FrameRate,
    [object]$HdrProfile,
    [string]$HdrOutput,
    [string]$DxgiOutput,
    [bool]$RequireMatchedDisplayRefreshRate
) {
    $expected = [ordered]@{
        codec = (Normalize-String $VideoStream.Codec).ToLowerInvariant()
        width = [int]$VideoStream.Width
        height = [int]$VideoStream.Height
        frameRate = [math]::Round($FrameRate, 3)
        hdrKind = $HdrProfile.kind
        hdrPlaybackStrategy = $HdrProfile.strategy
        isHdr = [bool]$HdrProfile.isHdr
        isDirectPlayable = [bool]$HdrProfile.isDirectPlayable
        isDolbyVision = [bool]$HdrProfile.isDolbyVision
        hdrOutput = $HdrOutput
        dxgiInput = if ($HdrProfile.isHdr) { 'YCBCR_STUDIO_G2084_TOPLEFT_P2020' } else { 'YCBCR_STUDIO_G22_LEFT_P709' }
        dxgiOutput = $DxgiOutput
        maxStartupDurationMs = 7000.0
        maxVideoStarvedPasses = 0
        maxAudioStarvedPasses = 0
        requireValidatedConversion = $true
        requireMatchedDisplayRefreshRate = $RequireMatchedDisplayRefreshRate
    }

    $videoRange = Normalize-String $VideoStream.VideoRange
    if ($videoRange.Equals('SDR', [StringComparison]::OrdinalIgnoreCase)) {
        if ($HdrProfile.kind -eq 'Hdr10') {
            $videoRange = 'HDR10'
        }
        elseif ($HdrProfile.kind -eq 'Hlg') {
            $videoRange = 'HLG'
        }
        elseif ($HdrProfile.kind -eq 'DolbyVisionWithHdr10Fallback') {
            $videoRange = 'HDR10 Dolby Vision'
        }
    }
    $colorPrimaries = Normalize-String $VideoStream.ColorPrimaries
    $colorTransfer = Normalize-String $VideoStream.ColorTransfer
    $colorSpace = Normalize-String $VideoStream.ColorSpace
    if (-not [string]::IsNullOrWhiteSpace($videoRange)) {
        $expected.videoRange = $videoRange
    }
    if (-not [string]::IsNullOrWhiteSpace($colorPrimaries)) {
        $expected.colorPrimaries = $colorPrimaries
    }
    if (-not [string]::IsNullOrWhiteSpace($colorTransfer)) {
        $expected.colorTransfer = $colorTransfer
    }
    if (-not [string]::IsNullOrWhiteSpace($colorSpace)) {
        $expected.colorSpace = $colorSpace
    }

    if ($HdrProfile.isDolbyVision) {
        $expected.dolbyVisionProfile = $HdrProfile.dolbyVisionProfile
        $expected.dolbyVisionCompatibilityId = $HdrProfile.dolbyVisionCompatibilityId
        $expected.hasHdr10BaseLayer = [bool]$HdrProfile.hasHdr10BaseLayer
        $expected.hasHlgBaseLayer = [bool]$HdrProfile.hasHlgBaseLayer
    }

    if ($RequireMatchedDisplayRefreshRate) {
        $expected.maxFrameGapMs = 104.3
        $expected.maxRenderIntervalMsP95 = 52.1
        $expected.maxRenderIntervalMsP99 = 83.4
        $expected.maxAudioVideoDriftMsP95 = 80.0
    }

    [pscustomobject]$expected
}

function New-ReferenceCase(
    [object]$Candidate,
    [string]$Suffix,
    [string[]]$Purpose,
    [int]$Tier,
    [bool]$ForceSdrOutput,
    [bool]$RequireMatchedDisplayRefreshRate,
    [long]$StartPositionTicks = 0,
    [double]$MaxSeekPositionErrorMs = 0
) {
    $category = if ($Tier -le 1) { 'stable' } else { 'challenge' }
    $severity = if (($Purpose -contains 'sdr-smoke') -or
        ($Purpose -contains 'hdr-output') -or
        ($Purpose -contains 'hdr-force-sdr') -or
        ($Purpose -contains 'dv-fallback') -or
        ($Purpose -contains 'cadence-23.976') -or
        ($Purpose -contains 'timeline')) {
        'high'
    }
    else {
        'medium'
    }
    $stability = if ($Tier -ge 3) { 'variable' } else { 'stable' }
    $hdrOutput = if ($ForceSdrOutput) { 'Sdr' } elseif ($Candidate.HdrProfile.isHdr) { 'Hdr10' } else { 'Sdr' }
    $dxgiOutput = if ($ForceSdrOutput -or -not $Candidate.HdrProfile.isHdr) {
        'RGB_FULL_G22_NONE_P709'
    }
    else {
        'RGB_FULL_G2084_NONE_P2020'
    }

    $expected = New-Expected `
        -VideoStream $Candidate.VideoStream `
        -FrameRate $Candidate.FrameRate `
        -HdrProfile $Candidate.HdrProfile `
        -HdrOutput $hdrOutput `
        -DxgiOutput $dxgiOutput `
        -RequireMatchedDisplayRefreshRate $RequireMatchedDisplayRefreshRate
    if ($MaxSeekPositionErrorMs -gt 0) {
        Add-Member -InputObject $expected -NotePropertyName 'maxSeekPositionErrorMs' -NotePropertyValue $MaxSeekPositionErrorMs -Force
    }
    if (($Purpose -contains 'audio-switch') -or ($Purpose -contains 'subtitle-switch')) {
        Add-Member -InputObject $expected `
            -NotePropertyName 'maxInteractionRecoveryDurationMs' `
            -NotePropertyValue 2000.0 `
            -Force
    }

    $scenario = if ($Purpose -contains 'timeline') {
        'timeline'
    }
    elseif ($Purpose -contains 'audio-switch') {
        'audio-switch'
    }
    elseif ($Purpose -contains 'subtitle-switch') {
        'subtitle-switch'
    }
    else {
        'playback'
    }

    $case = [ordered]@{
        caseId = ('private-emby/{0}/{1}/{2}' -f $Candidate.ItemId, $Candidate.MediaSourceId, $Suffix)
        category = $category
        severity = $severity
        stability = $stability
        uri = ('emby://items/{0}' -f $Candidate.ItemId)
        itemId = $Candidate.ItemId
        mediaSourceId = $Candidate.MediaSourceId
        forceSdrOutput = $ForceSdrOutput
        tier = $Tier
        executionRequirement = [pscustomobject][ordered]@{
            minimumEvidenceLevel = 'native-playback'
            scenario = $scenario
        }
        purpose = @($Purpose)
        expected = $expected
    }

    if ($StartPositionTicks -gt 0) {
        $case.startPositionTicks = $StartPositionTicks
    }

    [pscustomobject]$case
}

function New-ErrorHandlingReferenceCase {
    [pscustomobject][ordered]@{
        caseId = 'private-emby/error-handling/missing-file'
        category = 'stable'
        severity = 'medium'
        stability = 'stable'
        uri = 'emby://quality-cases/missing-file-error-handling'
        itemId = 'quality-case-missing-file-error-handling'
        mediaSourceId = 'quality-source-missing-file-error-handling'
        tier = 1
        executionRequirement = [pscustomobject][ordered]@{
            minimumEvidenceLevel = 'native-playback'
            scenario = 'playback'
        }
        purpose = @('error-handling')
        expected = [pscustomobject][ordered]@{
            codec = 'hevc'
            width = 1920
            height = 1080
            frameRate = 60.0
            hdrKind = 'Sdr'
            videoRange = 'SDR'
            colorPrimaries = 'bt709'
            colorTransfer = 'bt709'
            colorSpace = 'bt709'
            isDirectPlayable = $true
            expectedHdrOutput = 'Sdr'
            dxgiInputColorSpace = 'RGB_FULL_G22_NONE_P709'
            dxgiOutputColorSpace = 'RGB_FULL_G22_NONE_P709'
            requireValidatedConversion = $false
        }
    }
}

function ConvertTo-Candidates([object[]]$Items) {
    $candidates = @()
    foreach ($item in $Items) {
        $itemId = Normalize-String $item.Id
        if ([string]::IsNullOrWhiteSpace($itemId)) {
            continue
        }

        foreach ($source in (Get-Array $item.MediaSources)) {
            $sourceId = Normalize-String $source.Id
            $video = Get-VideoStream $source
            if ($null -eq $video -or [string]::IsNullOrWhiteSpace($sourceId)) {
                continue
            }

            $frameRate = Get-FrameRate $video
            if ($frameRate -le 0 -or [int]$video.Width -le 0 -or [int]$video.Height -le 0) {
                continue
            }

            $hdrProfile = Get-HdrProfile $video
            if ($hdrProfile.kind -eq 'Sdr' -and (Test-HasNameOnlyDolbyVisionHint $item $source $video)) {
                $hdrProfile = New-UnknownHdrProfileFromNameHint
            }

            $candidates += [pscustomobject]@{
                ItemId = $itemId
                MediaSourceId = $sourceId
                Bitrate = [long]$source.Bitrate
                VideoStream = $video
                FrameRate = $frameRate
                HdrProfile = $hdrProfile
            }
        }
    }

    $candidates
}

function Test-ItemHasMediaSources([object]$Item) {
    @(Get-Array $Item.MediaSources).Count -gt 0
}

function Set-ItemMediaSources([object]$Item, [object[]]$MediaSources) {
    $sources = @(Get-Array $MediaSources)
    if ($sources.Count -gt 0) {
        Add-Member -InputObject $Item -NotePropertyName 'MediaSources' -NotePropertyValue $sources -Force
    }

    $Item
}

function Expand-ItemsWithPlaybackInfoFiles([object[]]$Items, [string]$Directory) {
    if ([string]::IsNullOrWhiteSpace($Directory)) {
        return $Items
    }

    $expanded = @()
    foreach ($item in $Items) {
        if (Test-ItemHasMediaSources $item) {
            $expanded += $item
            continue
        }

        $itemId = Normalize-String $item.Id
        if ([string]::IsNullOrWhiteSpace($itemId)) {
            $expanded += $item
            continue
        }

        $playbackInfoPath = Join-Path $Directory ($itemId + '.json')
        if (-not (Test-Path -LiteralPath $playbackInfoPath)) {
            $expanded += $item
            continue
        }

        $playbackInfo = Get-Content -Raw -LiteralPath $playbackInfoPath | ConvertFrom-Json
        $expanded += Set-ItemMediaSources $item @(Get-Array $playbackInfo.MediaSources)
    }

    $expanded
}

function Expand-ItemsWithPlaybackInfoFromEmby(
    [object[]]$Items,
    [string]$UserId,
    [string]$AccessToken
) {
    $expanded = @()
    foreach ($item in $Items) {
        if (Test-ItemHasMediaSources $item) {
            $expanded += $item
            continue
        }

        $itemId = Normalize-String $item.Id
        if ([string]::IsNullOrWhiteSpace($itemId)) {
            $expanded += $item
            continue
        }

        try {
            $path = 'Items/{0}/PlaybackInfo?UserId={1}' -f
                [Uri]::EscapeDataString($itemId),
                [Uri]::EscapeDataString($UserId)
            $playbackInfo = Invoke-EmbyJson -Method 'Get' -Path $path -UserId $UserId -AccessToken $AccessToken
            $expanded += Set-ItemMediaSources $item @(Get-Array $playbackInfo.MediaSources)
        }
        catch {
            $expanded += $item
        }
    }

    $expanded
}

function Select-FirstCandidate([object[]]$Candidates, [scriptblock]$Predicate, [scriptblock]$SortExpression) {
    $Candidates |
        Where-Object -FilterScript $Predicate |
        Sort-Object -Property $SortExpression -Descending |
        Select-Object -First 1
}

function New-ReferenceManifest([object[]]$Items) {
    $candidates = @(ConvertTo-Candidates $Items)
    $cases = @()

    $sdr = Select-FirstCandidate $candidates { $_.HdrProfile.kind -eq 'Sdr' } { $_.Bitrate }
    if ($null -ne $sdr) {
        $cases += New-ReferenceCase $sdr 'sdr-smoke' @('sdr-smoke', 'av-sync', 'tracks', 'subtitles', 'end-of-stream') 1 $false $false
        $cases += New-ReferenceCase $sdr 'timeline' @('timeline') 1 $false $false -StartPositionTicks 600000000 -MaxSeekPositionErrorMs 500.0
        $cases += New-ReferenceCase $sdr 'audio-switch' @('tracks', 'audio-switch') 1 $false $false
        $cases += New-ReferenceCase $sdr 'subtitle-switch' @('subtitles', 'subtitle-switch') 1 $false $false
    }

    $hdr10 = Select-FirstCandidate $candidates { $_.HdrProfile.kind -eq 'Hdr10' -and [int]$_.VideoStream.Width -le 1920 } { $_.Bitrate }
    if ($null -eq $hdr10) {
        $hdr10 = Select-FirstCandidate $candidates { $_.HdrProfile.kind -eq 'Hdr10' } { $_.Bitrate }
    }

    if ($null -ne $hdr10) {
        $cases += New-ReferenceCase $hdr10 'hdr-output' @('hdr-output') 1 $false $false
        $cases += New-ReferenceCase $hdr10 'hdr-force-sdr' @('hdr-force-sdr') 2 $true $false
    }

    $buffering = Select-FirstCandidate $candidates {
        $_.HdrProfile.isHdr -eq $true -and
        $_.HdrProfile.isDirectPlayable -eq $true -and
        [int]$_.VideoStream.Width -ge 3840 -and
        $_.Bitrate -ge $MinBufferingBitrate
    } { $_.Bitrate }
    if ($null -ne $buffering) {
        $cases += New-ReferenceCase $buffering 'buffering' @('buffering') 3 $false $false
    }

    $dvReject = Select-FirstCandidate $candidates { $_.HdrProfile.kind -eq 'DolbyVisionUnsupported' } { $_.Bitrate }
    if ($null -ne $dvReject) {
        $cases += New-ReferenceCase $dvReject 'dv-reject' @('dv-reject', 'unsupported-source') 3 $false $false
    }

    $dvFallback = Select-FirstCandidate $candidates { $_.HdrProfile.kind -eq 'DolbyVisionWithHdr10Fallback' } { $_.Bitrate }
    if ($null -ne $dvFallback) {
        $cases += New-ReferenceCase $dvFallback 'dv-fallback' @('dv-fallback') 3 $false $false
    }

    $cadence = Select-FirstCandidate $candidates {
        $_.HdrProfile.isHdr -eq $true -and
        $_.HdrProfile.isDirectPlayable -eq $true -and
        (Test-IsCadence23976 $_.FrameRate)
    } { [int]$_.VideoStream.Width * [int]$_.VideoStream.Height }
    if ($null -ne $cadence) {
        $cases += New-ReferenceCase $cadence 'cadence-23976' @('cadence-23.976', 'frame-pacing') 2 $false $true
    }

    $cases += New-ErrorHandlingReferenceCase

    [pscustomobject][ordered]@{
        schemaVersion = 1
        cases = @($cases)
    }
}

function New-EmbyAuthorizationHeader([string]$UserId) {
    $identity = 'Client="Noira QA", Device="Codex", DeviceId="noira-player-qa", Version="0.1.0"'
    if ([string]::IsNullOrWhiteSpace($UserId)) {
        return 'Emby ' + $identity
    }

    return 'Emby UserId="' + $UserId + '", ' + $identity
}

function Invoke-EmbyJson(
    [string]$Method,
    [string]$Path,
    [object]$Body = $null,
    [string]$UserId = '',
    [string]$AccessToken = ''
) {
    $headers = @{
        Authorization = New-EmbyAuthorizationHeader $UserId
    }
    if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
        $headers['X-Emby-Token'] = $AccessToken
    }

    $uri = (Normalize-ServerUrl $ServerUrl) + '/' + $Path.TrimStart('/')
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }

    $json = $Body | ConvertTo-Json -Depth 8
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -Body $json -ContentType 'application/json'
}

function Add-QueryParameter([System.Collections.Generic.List[string]]$Parameters, [string]$Name, [object]$Value) {
    $text = Normalize-String $Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return
    }

    $Parameters.Add($Name + '=' + [Uri]::EscapeDataString($text))
}

function Read-ItemsFromEmby() {
    $normalizedServerUrl = Normalize-ServerUrl $ServerUrl
    if ([string]::IsNullOrWhiteSpace($normalizedServerUrl) -or
        [string]::IsNullOrWhiteSpace($UserName) -or
        [string]::IsNullOrWhiteSpace($Password)) {
        throw 'Set NOIRAPLAYER_QA_SERVER_URL, NOIRAPLAYER_QA_USERNAME, and NOIRAPLAYER_QA_PASSWORD, or pass -ItemsJsonPath for offline generation.'
    }

    $auth = Invoke-EmbyJson `
        -Method 'Post' `
        -Path 'Users/AuthenticateByName' `
        -Body ([pscustomobject]@{ Username = $UserName; Pw = $Password })
    $userId = Normalize-String $auth.User.Id
    $token = Normalize-String $auth.AccessToken
    if ([string]::IsNullOrWhiteSpace($userId) -or [string]::IsNullOrWhiteSpace($token)) {
        throw 'Emby authentication succeeded but did not return user id and access token.'
    }

    $items = @()
    $maxItems = [math]::Max(1, $Limit)
    $batchSize = [math]::Min(100, $maxItems)
    $startIndex = 0
    while ($items.Count -lt $maxItems) {
        $currentLimit = [math]::Min($batchSize, $maxItems - $items.Count)
        $parameters = [System.Collections.Generic.List[string]]::new()
        Add-QueryParameter $parameters 'Recursive' 'true'
        Add-QueryParameter $parameters 'IncludeItemTypes' 'Movie,Episode'
        Add-QueryParameter $parameters 'Fields' 'MediaSources,MediaStreams'
        Add-QueryParameter $parameters 'StartIndex' $startIndex
        Add-QueryParameter $parameters 'Limit' $currentLimit
        Add-QueryParameter $parameters 'SearchTerm' $SearchTerm

        $path = 'Users/{0}/Items?{1}' -f
            [Uri]::EscapeDataString($userId),
            ($parameters -join '&')
        $itemsDocument = Invoke-EmbyJson -Method 'Get' -Path $path -UserId $userId -AccessToken $token
        $batch = @(Get-Array $itemsDocument.Items)
        if ($batch.Count -eq 0) {
            break
        }

        $items += $batch
        $startIndex += $batch.Count
        if ($batch.Count -lt $currentLimit) {
            break
        }
    }

    @(Expand-ItemsWithPlaybackInfoFromEmby $items $userId $token)
}

if (-not [string]::IsNullOrWhiteSpace($ItemsJsonPath)) {
    $itemsDocument = Get-Content -Raw -LiteralPath $ItemsJsonPath | ConvertFrom-Json
    $items = @(Get-Array $itemsDocument.Items)
    $items = @(Expand-ItemsWithPlaybackInfoFiles $items $PlaybackInfoJsonDirectory)
}
else {
    $items = @(Read-ItemsFromEmby)
}

$manifest = New-ReferenceManifest $items
$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output ('wrote private Emby reference manifest: ' + $OutputPath)
Write-Output ('cases: ' + $manifest.cases.Count)
