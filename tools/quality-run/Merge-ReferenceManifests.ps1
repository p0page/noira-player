param(
    [Parameter(Mandatory = $true)]
    [string[]]$ManifestPath,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot 'docs\qa\private\merged-reference-manifest.local.json'
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

function Expand-ManifestPaths([string[]]$Values) {
    $paths = @()
    foreach ($value in $Values) {
        foreach ($path in ((Normalize-String $value).Split(','))) {
            $normalized = Normalize-String $path
            if (-not [string]::IsNullOrWhiteSpace($normalized)) {
                $paths += $normalized
            }
        }
    }

    $paths
}

if ($ManifestPath.Count -eq 0) {
    throw 'At least one manifest path is required.'
}

$manifestPaths = @(Expand-ManifestPaths $ManifestPath)
if ($manifestPaths.Count -eq 0) {
    throw 'At least one manifest path is required.'
}

$caseIds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$cases = @()
foreach ($path in $manifestPaths) {
    $resolvedPath = (Resolve-Path -LiteralPath $path).Path
    $manifest = Get-Content -Raw -LiteralPath $resolvedPath | ConvertFrom-Json
    $schemaVersion = Get-PropertyValue $manifest 'schemaVersion'
    if ($schemaVersion -ne 1) {
        throw ('Unsupported reference manifest schemaVersion in ' + $resolvedPath + ': ' + $schemaVersion)
    }

    foreach ($case in @($manifest.cases)) {
        $caseId = Normalize-String (Get-PropertyValue $case 'caseId')
        if ([string]::IsNullOrWhiteSpace($caseId)) {
            throw ('Reference manifest case is missing caseId in ' + $resolvedPath)
        }

        if (-not $caseIds.Add($caseId)) {
            throw ('Reference manifest caseId is duplicated across inputs: ' + $caseId)
        }

        $cases += $case
    }
}

$mergedManifest = [pscustomobject]@{
    schemaVersion = 1
    cases = $cases
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$mergedManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output ('wrote merged reference manifest: ' + $OutputPath)
Write-Output ('cases: ' + $cases.Count)
$global:LASTEXITCODE = 0
