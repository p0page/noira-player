$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scriptPath = Join-Path $PSScriptRoot 'New-PrivateEmbyReferenceManifest.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('private-emby-manifest-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $itemsPath = Join-Path $tempRoot 'items.json'
    $outputPath = Join-Path $tempRoot 'manifest.local.json'
    $validationPath = Join-Path $tempRoot 'manifest-validation.json'
    $itemsWithoutSourcesPath = Join-Path $tempRoot 'items-without-sources.json'
    $playbackInfoDir = Join-Path $tempRoot 'playback-info'
    $expandedOutputPath = Join-Path $tempRoot 'expanded-manifest.local.json'
    $hdrOnlyItemsPath = Join-Path $tempRoot 'hdr-only-items.json'
    $hdrOnlyOutputPath = Join-Path $tempRoot 'hdr-only-manifest.local.json'
    New-Item -ItemType Directory -Path $playbackInfoDir | Out-Null

    @'
{
  "Items": [
    {
      "Id": "sdr-movie",
      "Name": "SDR Sample",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "sdr-source",
          "Name": "1080p SDR",
          "Bitrate": 3000000,
          "RunTimeTicks": 6000000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Codec": "hevc",
              "Width": 1920,
              "Height": 1080,
              "RealFrameRate": 60.0,
              "VideoRange": "SDR",
              "ColorPrimaries": "bt709",
              "ColorTransfer": "bt709",
              "ColorSpace": "bt709"
            }
          ]
        }
      ]
    },
    {
      "Id": "hdr10-movie",
      "Name": "HDR10 Sample",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "hdr10-source",
          "Name": "1080p HDR10",
          "Bitrate": 10000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Codec": "hevc",
              "Width": 1920,
              "Height": 1080,
              "RealFrameRate": 60.0,
              "VideoRange": "SDR",
              "ColorPrimaries": "bt2020",
              "ColorTransfer": "smpte2084",
              "ColorSpace": "bt2020nc"
            }
          ]
        }
      ]
    },
    {
      "Id": "track-movie",
      "Name": "Multi-track SDR Sample",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "track-source",
          "Name": "1080p SDR with alternate tracks",
          "Bitrate": 2500000,
          "RunTimeTicks": 6000000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Index": 0,
              "Codec": "hevc",
              "Width": 1920,
              "Height": 1080,
              "RealFrameRate": 60.0,
              "VideoRange": "SDR",
              "ColorPrimaries": "bt709",
              "ColorTransfer": "bt709",
              "ColorSpace": "bt709"
            },
            {
              "Type": "Audio",
              "Index": 1,
              "Codec": "aac"
            },
            {
              "Type": "Audio",
              "Index": 2,
              "Codec": "ac3"
            },
            {
              "Type": "Subtitle",
              "Index": 3,
              "Codec": "subrip"
            }
          ]
        }
      ]
    },
    {
      "Id": "buffering-movie",
      "Name": "4K High Bitrate HDR",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "buffering-source",
          "Name": "4K HDR10 90M",
          "Bitrate": 90000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Codec": "hevc",
              "Width": 3840,
              "Height": 2160,
              "RealFrameRate": 60.0,
              "VideoRange": "HDR10",
              "ColorPrimaries": "bt2020",
              "ColorTransfer": "smpte2084",
              "ColorSpace": "bt2020nc"
            }
          ]
        }
      ]
    },
    {
      "Id": "cadence-movie",
      "Name": "23.976 HDR10 Sample",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "cadence-source",
          "Name": "4K HDR10 23.976",
          "Bitrate": 45000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Codec": "hevc",
              "Width": 3840,
              "Height": 2160,
              "RealFrameRate": 23.976,
              "VideoRange": "HDR10",
              "ColorPrimaries": "bt2020",
              "ColorTransfer": "smpte2084",
              "ColorSpace": "bt2020nc"
            }
          ]
        }
      ]
    },
    {
      "Id": "dv5-movie",
      "Name": "Dolby Vision P5",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "dv5-source",
          "Name": "4K DV P5",
          "Bitrate": 50000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Codec": "dvhe.05",
              "Width": 3840,
              "Height": 2160,
              "RealFrameRate": 60.0,
              "VideoRange": "Dolby Vision"
            }
          ]
        }
      ]
    },
    {
      "Id": "dv81-movie",
      "Name": "Dolby Vision P8.1",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "dv81-source",
          "Name": "4K DoVi HDR10",
          "Bitrate": 55000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Codec": "dvhe.08.01",
              "Width": 3840,
              "Height": 2160,
              "RealFrameRate": 60.0,
              "VideoRange": "HDR10 Dolby Vision",
              "ColorPrimaries": "bt2020",
              "ColorTransfer": "smpte2084",
              "ColorSpace": "bt2020nc"
            }
          ]
        }
      ]
    },
    {
      "Id": "name-only-dv",
      "Name": "Misleading DV P5 Name",
      "Type": "Movie",
      "MediaSources": [
        {
          "Id": "name-only-source",
          "Name": "4K DV P5 by name only",
          "Bitrate": 12000000,
          "MediaStreams": [
            {
              "Type": "Video",
              "Codec": "hevc",
              "Width": 3840,
              "Height": 2160,
              "RealFrameRate": 60.0,
              "VideoRange": "SDR",
              "DisplayTitle": "HEVC DV Profile 5 by title only"
            }
          ]
        }
      ]
    }
  ]
}
'@ | Set-Content -LiteralPath $itemsPath -Encoding UTF8

    @'
{
  "Items": [
    {
      "Id": "playback-info-sdr",
      "Name": "PlaybackInfo SDR",
      "Type": "Movie"
    }
  ]
}
'@ | Set-Content -LiteralPath $itemsWithoutSourcesPath -Encoding UTF8

    @'
{
  "MediaSources": [
    {
      "Id": "playback-info-sdr-source",
      "Name": "1080p SDR from PlaybackInfo",
      "Bitrate": 3000000,
      "MediaStreams": [
        {
          "Type": "Video",
          "Codec": "hevc",
          "Width": 1920,
          "Height": 1080,
          "RealFrameRate": 60.0,
          "VideoRange": "SDR",
          "ColorPrimaries": "bt709",
          "ColorTransfer": "bt709",
          "ColorSpace": "bt709"
        }
      ]
    }
  ]
}
'@ | Set-Content -LiteralPath (Join-Path $playbackInfoDir 'playback-info-sdr.json') -Encoding UTF8

    Push-Location $repoRoot
    try {
        & $scriptPath `
            -ItemsJsonPath $itemsWithoutSourcesPath `
            -PlaybackInfoJsonDirectory $playbackInfoDir `
            -OutputPath $expandedOutputPath `
            -ServerUrl 'https://emby.example:443' `
            -UserName 'private-user' `
            -Password 'private-password'
        if (-not (Test-Path -LiteralPath $expandedOutputPath)) {
            throw 'New-PrivateEmbyReferenceManifest.ps1 did not write the expected PlaybackInfo-expanded manifest.'
        }

        $expandedManifest = Get-Content -Raw -LiteralPath $expandedOutputPath | ConvertFrom-Json
        if (-not ($expandedManifest.cases | Where-Object {
            $_.caseId -eq 'private-emby/playback-info-sdr/playback-info-sdr-source/sdr-smoke' -and
            ($_.purpose -contains 'sdr-smoke') -and
            $_.expected.hdrKind -eq 'Sdr' -and
            $_.expected.videoRange -eq 'SDR' -and
            $_.expected.colorPrimaries -eq 'bt709' -and
            $_.expected.colorTransfer -eq 'bt709' -and
            $_.expected.colorSpace -eq 'bt709'
        })) {
            throw 'Generated manifest should use PlaybackInfo media sources when Items do not include MediaSources.'
        }

        & $scriptPath `
            -ItemsJsonPath $itemsPath `
            -OutputPath $outputPath `
            -ServerUrl 'https://emby.example:443' `
            -UserName 'private-user' `
            -Password 'private-password'
        if (-not (Test-Path -LiteralPath $outputPath)) {
            throw 'New-PrivateEmbyReferenceManifest.ps1 did not write the expected manifest.'
        }

        dotnet run `
            --project tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj `
            --no-build `
            -- validate-manifest `
            --manifest $outputPath `
            --output $validationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Generated private Emby manifest did not pass validate-manifest.'
        }
    }
    finally {
        Pop-Location
    }

    $manifestText = Get-Content -Raw -LiteralPath $outputPath
    if ($manifestText.Contains('emby.example') -or
        $manifestText.Contains('private-user') -or
        $manifestText.Contains('private-password')) {
        throw 'Generated manifest must not contain private server URL, username, or password.'
    }

    $manifest = $manifestText | ConvertFrom-Json

    $hdrOnlyItems = Get-Content -Raw -LiteralPath $itemsPath | ConvertFrom-Json
    $hdrOnlyItems.Items = @($hdrOnlyItems.Items | Where-Object {
        $_.Id -notin @('sdr-movie', 'track-movie')
    })
    $hdrOnlyItems | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $hdrOnlyItemsPath -Encoding UTF8
    & $scriptPath -ItemsJsonPath $hdrOnlyItemsPath -OutputPath $hdrOnlyOutputPath
    $hdrOnlyManifest = Get-Content -Raw -LiteralPath $hdrOnlyOutputPath | ConvertFrom-Json
    if (-not ($hdrOnlyManifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/buffering-movie/buffering-source/long-pause-resume' -and
        $_.expected.hdrKind -eq 'Hdr10' -and
        $_.expected.isDirectPlayable -eq $true -and
        $_.pauseSeconds -eq 30
    })) {
        throw 'Long pause generation should fall back to the highest-bitrate direct-playable HDR source when no SDR source exists.'
    }

    $audioSwitchCase = @($manifest.cases | Where-Object {
        $_.executionRequirement.scenario -eq 'audio-switch'
    }) | Select-Object -First 1
    $subtitleSwitchCase = @($manifest.cases | Where-Object {
        $_.executionRequirement.scenario -eq 'subtitle-switch'
    }) | Select-Object -First 1
    if ($null -eq $audioSwitchCase -or
        $audioSwitchCase.caseId -ne 'private-emby/track-movie/track-source/audio-switch' -or
        $audioSwitchCase.expected.maxInteractionRecoveryDurationMs -ne 2000 -or
        -not ($audioSwitchCase.purpose -contains 'audio-switch')) {
        throw 'Generated private Emby manifest must bind audio-switch to a source with at least two audio tracks.'
    }
    if ($null -eq $subtitleSwitchCase -or
        $subtitleSwitchCase.caseId -ne 'private-emby/track-movie/track-source/subtitle-switch' -or
        $subtitleSwitchCase.expected.maxInteractionRecoveryDurationMs -ne 2000 -or
        -not ($subtitleSwitchCase.purpose -contains 'subtitle-switch')) {
        throw 'Generated private Emby manifest must bind subtitle-switch to a source with a subtitle track.'
    }
    if ($manifest.cases | Where-Object {
        $_.itemId -eq 'sdr-movie' -and
        $_.executionRequirement.scenario -in @('audio-switch', 'subtitle-switch')
    }) {
        throw 'Generated private Emby manifest must not declare interaction scenarios on an incapable source.'
    }
    $validation = Get-Content -Raw -LiteralPath $validationPath | ConvertFrom-Json
    if ($validation.coverage.status -ne 'incomplete' -or
        $validation.coverage.isCoreEvaluationReady -ne $false -or
        -not ($validation.coverage.missingPurposes -contains 'error-handling')) {
        throw 'A private Emby source manifest may be partial; deterministic missing-file coverage belongs to the local native suite.'
    }

    $requiredPurposes = @(
        'sdr-smoke',
        'hdr-output',
        'hdr-force-sdr',
        'dv-reject',
        'dv-fallback',
        'cadence-23.976',
        'frame-pacing',
        'av-sync',
        'buffering',
        'timeline',
        'tracks',
        'subtitles',
        'end-of-stream'
    )
    foreach ($purpose in $requiredPurposes) {
        if (-not ($validation.coverage.coveredPurposes -contains $purpose)) {
            throw ('Generated manifest is missing purpose: ' + $purpose)
        }
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/dv5-movie/dv5-source/dv-reject' -and
        $_.category -eq 'challenge' -and
        $_.severity -eq 'medium' -and
        $_.stability -eq 'variable' -and
            $_.expected.hdrKind -eq 'DolbyVisionUnsupported' -and
            $_.expected.videoRange -eq 'Dolby Vision' -and
            $_.expected.dolbyVisionProfile -eq 5 -and
            $_.expected.isDirectPlayable -eq $false
    })) {
        throw 'Generated manifest should include a DV Profile 5 reject case from stream metadata.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/hdr10-movie/hdr10-source/hdr-output' -and
        $_.expected.hdrKind -eq 'Hdr10' -and
        $_.expected.videoRange -eq 'HDR10' -and
        $_.expected.colorTransfer -eq 'smpte2084'
    })) {
        throw 'PQ/BT.2020 sources must normalize a contradictory SDR VideoRange to HDR10.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/sdr-movie/sdr-source/timeline' -and
        ($_.purpose -contains 'timeline') -and
        $_.executionRequirement.scenario -eq 'timeline' -and
        $_.startPositionTicks -eq 600000000 -and
        $_.seekTargetPositionTicks -eq 3000000000 -and
        ($_.seekTargetPositionTicks - $_.startPositionTicks) -ge 300000000 -and
        $_.seekTargetPositionTicks -le 5700000000 -and
        $_.expected.maxSeekPositionErrorMs -eq 500
    })) {
        throw 'Generated manifest should include a duration-derived long-distance timeline case with position threshold metadata.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/sdr-movie/sdr-source/sdr-smoke' -and
        $_.category -eq 'stable' -and
        $_.severity -eq 'high' -and
            $_.stability -eq 'stable' -and
            ($_.purpose -contains 'tracks') -and
            ($_.purpose -contains 'subtitles') -and
            -not ($_.purpose -contains 'end-of-stream') -and
            $_.expected.videoRange -eq 'SDR' -and
            $_.expected.colorPrimaries -eq 'bt709' -and
            $_.expected.colorTransfer -eq 'bt709' -and
            $_.expected.colorSpace -eq 'bt709'
    })) {
        throw 'Generated manifest should keep stable track and subtitle coverage on the SDR smoke playback case.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/sdr-movie/sdr-source/end-of-stream' -and
        $_.category -eq 'stable' -and
        $_.severity -eq 'high' -and
        $_.stability -eq 'stable' -and
        $_.executionRequirement.scenario -eq 'end-of-stream' -and
        $_.startPositionTicks -eq 5950000000 -and
        ($_.purpose -contains 'end-of-stream') -and
        $_.purpose.Count -eq 1
    })) {
        throw 'Generated manifest should include a dedicated stable end-of-stream execution case.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/sdr-movie/sdr-source/long-pause-resume' -and
        $_.category -eq 'challenge' -and
        $_.severity -eq 'high' -and
        $_.stability -eq 'stable' -and
        $_.executionRequirement.scenario -eq 'pause-resume' -and
        $_.pauseSeconds -eq 30 -and
        ($_.purpose -contains 'pause-resume') -and
        ($_.purpose -contains 'network-recovery')
    })) {
        throw 'Generated manifest should include a dedicated 30 second private Emby pause/resume challenge case.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/dv81-movie/dv81-source/dv-fallback' -and
        $_.category -eq 'challenge' -and
        $_.severity -eq 'high' -and
            $_.stability -eq 'variable' -and
            $_.expected.hdrKind -eq 'DolbyVisionWithHdr10Fallback' -and
            $_.expected.videoRange -eq 'HDR10 Dolby Vision' -and
            $_.expected.colorPrimaries -eq 'bt2020' -and
            $_.expected.colorTransfer -eq 'smpte2084' -and
            $_.expected.colorSpace -eq 'bt2020nc' -and
            $_.expected.dolbyVisionCompatibilityId -eq 1 -and
            $_.expected.hasHdr10BaseLayer -eq $true
    })) {
        throw 'Generated manifest should include a DV Profile 8.1 HDR10 fallback case from stream metadata.'
    }

    if ($manifest.cases | Where-Object { $_.itemId -eq 'name-only-dv' -and $_.expected.isDolbyVision -eq $true }) {
        throw 'Generated manifest must not classify Dolby Vision from item/source names or display titles alone.'
    }

    if ($manifest.cases | Where-Object { $_.itemId -eq 'name-only-dv' -and $_.expected.hdrKind -eq 'Sdr' }) {
        throw 'Generated manifest must not select name/title-only Dolby Vision hints as SDR evidence.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/cadence-movie/cadence-source/cadence-23976' -and
        $_.category -eq 'challenge' -and
        $_.severity -eq 'high' -and
            $_.stability -eq 'stable' -and
            $_.expected.frameRate -eq 23.976 -and
            $_.expected.videoRange -eq 'HDR10' -and
            $_.expected.colorPrimaries -eq 'bt2020' -and
            $_.expected.colorTransfer -eq 'smpte2084' -and
            $_.expected.colorSpace -eq 'bt2020nc' -and
            $_.expected.requireMatchedDisplayRefreshRate -eq $true
    })) {
        throw 'Generated manifest should include a 23.976 cadence case.'
    }

    if (-not ($manifest.cases | Where-Object {
        $_.forceSdrOutput -eq $true -and
        $_.expected.hdrKind -eq 'Hdr10' -and
        $_.expected.hdrOutput -eq 'Hdr10' -and
        $_.expected.sdrDisplayFallback.hdrOutput -eq 'Sdr' -and
        ($_.expected.sdrDisplayFallback.dxgiInputAnyOf -contains 'YCBCR_STUDIO_G22_LEFT_P2020') -and
        $_.expected.sdrDisplayFallback.requiredConversionStatus -eq 'tone-mapped-hable' -and
        $_.expected.videoRange -eq 'HDR10' -and
        $_.expected.colorPrimaries -eq 'bt2020' -and
        $_.expected.colorTransfer -eq 'smpte2084' -and
        $_.expected.colorSpace -eq 'bt2020nc'
    })) {
        throw 'Generated manifest should include an HDR force-SDR case.'
    }

    if ($manifest.cases | Where-Object {
        $_.caseId -eq 'private-emby/error-handling/missing-file' -or
        $_.uri -eq 'emby://quality-cases/missing-file-error-handling'
    }) {
        throw 'Private Emby manifests must not synthesize a fake Emby item for missing-file coverage; the local native suite owns that deterministic case.'
    }

    Write-Output 'private-emby-reference-manifest tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
