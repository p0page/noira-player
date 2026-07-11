param(
    [string]$Url = '',
    [switch]$Clear,
    [string]$PackagesRoot = (Join-Path $env:LOCALAPPDATA 'Packages'),
    [string]$PackageNamePrefix = 'NoiraPlayer.App_',
    [string]$SummaryPath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PackagesRoot)) {
    throw 'PackagesRoot is required.'
}

if (-not (Test-Path -LiteralPath $PackagesRoot)) {
    throw ('Packages root not found: ' + $PackagesRoot)
}

if ($Clear -and -not [string]::IsNullOrWhiteSpace($Url)) {
    throw 'Specify either Url or Clear, not both.'
}

$normalizedUrl = ''
if (-not $Clear) {
    if ([string]::IsNullOrWhiteSpace($Url)) {
        throw 'Url is required unless Clear is specified.'
    }

    $uri = $null
    if (-not [uri]::TryCreate($Url.Trim(), [System.UriKind]::Absolute, [ref]$uri) -or
        ($uri.Scheme -ne [uri]::UriSchemeHttp -and $uri.Scheme -ne [uri]::UriSchemeHttps)) {
        throw 'WebView dev server URL must be an absolute HTTP or HTTPS URL.'
    }

    $normalizedUrl = $uri.AbsoluteUri
}

$packageRoot = Get-ChildItem -LiteralPath $PackagesRoot -Directory |
    Where-Object { $_.Name -like ($PackageNamePrefix + '*') } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $packageRoot) {
    throw ('Package folder not found under: ' + $PackagesRoot)
}

$localState = Join-Path $packageRoot.FullName 'LocalState'
New-Item -ItemType Directory -Path $localState -Force | Out-Null
$devUrlPath = Join-Path $localState 'webview-dev-url.txt'

if ($Clear) {
    Remove-Item -LiteralPath $devUrlPath -Force -ErrorAction SilentlyContinue
    $action = 'clear'
}
else {
    Set-Content -LiteralPath $devUrlPath -Value $normalizedUrl -Encoding UTF8
    $action = 'set'
}

$summary = [pscustomobject]@{
    schemaVersion = 1
    action = $action
    url = $normalizedUrl
    packageRoot = $packageRoot.FullName
    localState = $localState
    devUrlPath = $devUrlPath
}

$summaryJson = $summary | ConvertTo-Json -Depth 4
if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    Write-Output $summaryJson
}
else {
    $summaryDirectory = Split-Path -Parent $SummaryPath
    if (-not [string]::IsNullOrWhiteSpace($summaryDirectory)) {
        New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
    }

    Set-Content -LiteralPath $SummaryPath -Value $summaryJson -Encoding UTF8
}
