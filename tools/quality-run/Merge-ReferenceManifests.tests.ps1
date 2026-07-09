$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scriptPath = Join-Path $PSScriptRoot 'Merge-ReferenceManifests.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('merge-reference-manifests-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw 'Merge-ReferenceManifests.ps1 must exist.'
    }

    $manifestAPath = Join-Path $tempRoot 'manifest-a.json'
    $manifestBPath = Join-Path $tempRoot 'manifest-b.json'
    $duplicateManifestPath = Join-Path $tempRoot 'manifest-duplicate.json'
    $mergedPath = Join-Path $tempRoot 'merged.local.json'
    $validationPath = Join-Path $tempRoot 'merged-validation.json'
    $duplicateOutputPath = Join-Path $tempRoot 'duplicate-output.local.json'
    $skipDuplicateOutputPath = Join-Path $tempRoot 'skip-duplicate-output.local.json'

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "public/sdr-smoke",
      "uri": "https://media.example/sdr.mp4",
      "tier": 1,
      "purpose": [
        "sdr-smoke",
        "av-sync"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 1080,
        "frameRate": 60.0,
        "hdrKind": "Sdr"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $manifestAPath -Encoding UTF8

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "private/hdr10-cadence",
      "uri": "emby://quality-cases/hdr10-cadence",
      "itemId": "placeholder-item",
      "mediaSourceId": "placeholder-source",
      "tier": 2,
      "purpose": [
        "hdr-output",
        "cadence-23.976",
        "frame-pacing"
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
'@ | Set-Content -LiteralPath $manifestBPath -Encoding UTF8

    & $scriptPath `
        -ManifestPath $manifestAPath, $manifestBPath `
        -OutputPath $mergedPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Merge-ReferenceManifests.ps1 returned a non-zero exit code for unique manifests.'
    }

    $merged = Get-Content -Raw -LiteralPath $mergedPath | ConvertFrom-Json
    if ($merged.schemaVersion -ne 1 -or $merged.cases.Count -ne 2) {
        throw 'Merged manifest should use schemaVersion 1 and contain both cases.'
    }

    if ($merged.cases[0].caseId -ne 'public/sdr-smoke' -or
        $merged.cases[1].caseId -ne 'private/hdr10-cadence') {
        throw 'Merged manifest should preserve input manifest case order.'
    }

    $commaSeparatedMergedPath = Join-Path $tempRoot 'merged-comma.local.json'
    & $scriptPath `
        -ManifestPath ($manifestAPath + ',' + $manifestBPath) `
        -OutputPath $commaSeparatedMergedPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Merge-ReferenceManifests.ps1 returned a non-zero exit code for comma-separated manifest paths.'
    }

    $commaSeparatedMerged = Get-Content -Raw -LiteralPath $commaSeparatedMergedPath | ConvertFrom-Json
    if ($commaSeparatedMerged.cases.Count -ne 2) {
        throw 'Merged manifest should support comma-separated manifest path arguments.'
    }

    Push-Location $repoRoot
    try {
        dotnet run `
            --project tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj `
            -- validate-manifest `
            --manifest $mergedPath `
            --output $validationPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Merged manifest did not pass validate-manifest.'
        }
    }
    finally {
        Pop-Location
    }

    @'
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "public/sdr-smoke",
      "uri": "https://media.example/duplicate.mp4",
      "tier": 1,
      "purpose": [
        "sdr-smoke"
      ],
      "expected": {
        "codec": "hevc",
        "width": 1920,
        "height": 1080,
        "frameRate": 60.0,
        "hdrKind": "Sdr"
      }
    }
  ]
}
'@ | Set-Content -LiteralPath $duplicateManifestPath -Encoding UTF8

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $duplicateCommand = "& '{0}' -ManifestPath @('{1}', '{2}') -OutputPath '{3}'" -f `
            $scriptPath.Replace("'", "''"),
            $manifestAPath.Replace("'", "''"),
            $duplicateManifestPath.Replace("'", "''"),
            $duplicateOutputPath.Replace("'", "''")
        $duplicateOutput = & powershell `
            -NoProfile `
            -ExecutionPolicy Bypass `
            -Command $duplicateCommand 2>&1
        $duplicateExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($duplicateExitCode -eq 0) {
        throw 'Merge-ReferenceManifests.ps1 should fail for duplicate caseId values.'
    }

    if (($duplicateOutput -join "`n") -notmatch 'public/sdr-smoke') {
        throw 'Duplicate caseId failure should name the duplicated caseId.'
    }

    if (Test-Path -LiteralPath $duplicateOutputPath) {
        throw 'Merge-ReferenceManifests.ps1 should not write merged output when duplicate caseId values are present.'
    }

    & $scriptPath `
        -ManifestPath $manifestAPath, $duplicateManifestPath `
        -OutputPath $skipDuplicateOutputPath `
        -DuplicateCaseIdMode 'skip'
    if ($LASTEXITCODE -ne 0) {
        throw 'Merge-ReferenceManifests.ps1 should support explicitly skipping duplicate caseId values.'
    }

    $skipDuplicateMerged = Get-Content -Raw -LiteralPath $skipDuplicateOutputPath | ConvertFrom-Json
    if ($skipDuplicateMerged.cases.Count -ne 1) {
        throw 'Skipping duplicate caseId values should keep only the first case.'
    }

    if ($skipDuplicateMerged.cases[0].uri -ne 'https://media.example/sdr.mp4') {
        throw 'Skipping duplicate caseId values should preserve the first matching case.'
    }

    Write-Output 'merge-reference-manifests tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
