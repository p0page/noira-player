param(
    [string]$PackagesRoot = (Join-Path $env:LOCALAPPDATA 'Packages'),
    [string]$PackageNamePrefix = 'NextGenEmby.App_',
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$SummaryPath = ''
)

$ErrorActionPreference = 'Stop'

function Get-RelativePath(
    [string]$Root,
    [string]$Path
) {
    $rootFullPath = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $pathFullPath = [System.IO.Path]::GetFullPath($Path)
    $rootUri = New-Object System.Uri($rootFullPath)
    $pathUri = New-Object System.Uri($pathFullPath)
    [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()) -replace '\\', '/'
}

if ([string]::IsNullOrWhiteSpace($PackagesRoot)) {
    throw 'PackagesRoot is required.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    throw 'OutputDirectory is required.'
}

if (-not (Test-Path -LiteralPath $PackagesRoot)) {
    throw ('Packages root not found: ' + $PackagesRoot)
}

$packageRoot = Get-ChildItem -LiteralPath $PackagesRoot -Directory |
    Where-Object { $_.Name -like ($PackageNamePrefix + '*') } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $packageRoot) {
    throw ('Package folder not found under: ' + $PackagesRoot)
}

$capturedRoot = Join-Path $packageRoot.FullName 'LocalState\quality-run\captured'
if (-not (Test-Path -LiteralPath $capturedRoot)) {
    throw ('Captured quality-run directory not found: ' + $capturedRoot)
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$reports = @()
foreach ($report in Get-ChildItem -LiteralPath $capturedRoot -Filter '*.json' -File -Recurse) {
    $relativePath = Get-RelativePath -Root $capturedRoot -Path $report.FullName
    $destination = Join-Path $OutputDirectory ($relativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $destinationDirectory = Split-Path -Parent $destination
    if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $report.FullName -Destination $destination -Force
    $reports += [pscustomobject]@{
        relativePath = $relativePath
        sourcePath = $report.FullName
        outputPath = $destination
    }
}

$summary = [pscustomobject]@{
    schemaVersion = 1
    packageRoot = $packageRoot.FullName
    capturedRoot = $capturedRoot
    outputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
    exportedReportCount = $reports.Count
    reports = $reports
}

$json = $summary | ConvertTo-Json -Depth 6
if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    Write-Output $json
}
else {
    $summaryDirectory = Split-Path -Parent $SummaryPath
    if (-not [string]::IsNullOrWhiteSpace($summaryDirectory)) {
        New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
    }

    Set-Content -LiteralPath $SummaryPath -Value $json -Encoding UTF8
}
