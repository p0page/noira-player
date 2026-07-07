$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Test-PublicReferenceMedia.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('public-reference-media-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw 'Test-PublicReferenceMedia.ps1 must exist.'
    }

    $manifestPath = Join-Path $tempRoot 'reference-manifest.json'
    $mismatchManifestPath = Join-Path $tempRoot 'mismatch-reference-manifest.json'
    $outputPath = Join-Path $tempRoot 'public-reference-media.local.json'
    $mismatchOutputPath = Join-Path $tempRoot 'public-reference-media-mismatch.local.json'
    $fakeProbePath = Join-Path $tempRoot 'fake-ffprobe.ps1'

    @'
param()

$url = [string]$args[$args.Count - 1]
$width = 3840
if ($url -match 'mismatch') {
    $width = 3840
}

@"
{
  "streams": [
    {
      "codec_name": "hevc",
      "profile": "Main 10",
      "width": $width,
      "height": 2160,
      "pix_fmt": "yuv420p10le",
      "color_space": "bt2020nc",
      "color_transfer": "smpte2084",
      "color_primaries": "bt2020",
      "r_frame_rate": "60/1",
      "avg_frame_rate": "60/1"
    }
  ],
  "format": {
    "duration": "29.950000",
    "bit_rate": "49219427"
  }
}
"@
'@ | Set-Content -LiteralPath $fakeProbePath -Encoding UTF8

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "public/hdr10-4k60",
      "uri": "https://media.example/hdr10-4k60.mp4",
      "tier": 3,
      "purpose": [
        "hdr-output",
        "buffering"
      ],
      "expected": {
        "codec": "hevc",
        "width": 3840,
        "height": 2160,
        "frameRate": 60.0,
        "hdrKind": "Hdr10"
      }
    },
    {
      "caseId": "local/emby-bound",
      "uri": "emby://quality-cases/local-only",
      "tier": 2,
      "purpose": [
        "cadence-23.976"
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
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $passOutput = & powershell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $scriptPath `
        -ManifestPath $manifestPath `
        -OutputPath $outputPath `
        -FfprobePath $fakeProbePath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ("Expected public reference media probe to pass. Output:`n" + ($passOutput -join "`n"))
    }

    $report = Get-Content -Raw -LiteralPath $outputPath | ConvertFrom-Json
    if ($report.result -ne 'pass') {
        throw 'Expected public reference media probe report result to pass.'
    }

    if ($report.caseCount -ne 2 -or $report.probedCaseCount -ne 1) {
        throw 'Expected public reference media probe to count all cases but probe only public HTTP(S) cases.'
    }

    $publicCase = $report.cases | Where-Object { $_.caseId -eq 'public/hdr10-4k60' } | Select-Object -First 1
    if (-not $publicCase -or $publicCase.status -ne 'pass') {
        throw 'Expected public HDR10 case to pass.'
    }

    if ($publicCase.caseId -is [System.Array]) {
        throw 'Expected public HDR10 caseId to be a scalar string.'
    }

    foreach ($signal in @('source.codec', 'source.width', 'source.height', 'source.frameRate', 'source.hdrKind')) {
        if (-not ($publicCase.checks | Where-Object { $_.signal -eq $signal -and $_.status -eq 'pass' })) {
            throw ('Expected public HDR10 case to pass signal: ' + $signal)
        }
    }

    $localCase = $report.cases | Where-Object { $_.caseId -eq 'local/emby-bound' } | Select-Object -First 1
    if (-not $localCase -or $localCase.status -ne 'skipped' -or $localCase.reason -ne 'non-public-uri') {
        throw 'Expected local Emby-bound case to be skipped as non-public-uri.'
    }

    if ($localCase.caseId -is [System.Array]) {
        throw 'Expected local Emby-bound caseId to be a scalar string.'
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "public/mismatch",
      "uri": "https://media.example/mismatch.mp4",
      "tier": 3,
      "purpose": [
        "hdr-output"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 2160,
        "frameRate": 60.0,
        "hdrKind": "Hdr10"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $mismatchManifestPath -Encoding UTF8

    $failOutput = & powershell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $scriptPath `
        -ManifestPath $mismatchManifestPath `
        -OutputPath $mismatchOutputPath `
        -FfprobePath $fakeProbePath 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw ("Expected public reference media probe to fail on mismatched metadata. Output:`n" + ($failOutput -join "`n"))
    }

    $mismatchReport = Get-Content -Raw -LiteralPath $mismatchOutputPath | ConvertFrom-Json
    $mismatchCase = $mismatchReport.cases | Where-Object { $_.caseId -eq 'public/mismatch' } | Select-Object -First 1
    if ($mismatchReport.result -ne 'fail' -or -not $mismatchCase) {
        throw 'Expected mismatch report result and case to fail.'
    }

    if (-not ($mismatchCase.checks | Where-Object {
        $_.signal -eq 'source.width' -and
        $_.status -eq 'fail' -and
        $_.expected -eq 1920 -and
        $_.actual -eq 3840
    })) {
        throw 'Expected mismatch report to expose failed source.width signal.'
    }

    Write-Output 'public-reference-media tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
