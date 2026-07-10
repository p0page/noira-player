$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Test-NoiraHybridTransport.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw 'Hybrid transport probe script is missing.'
}

. $scriptPath -LoadFunctionsOnly

$probeSource = Get-Content -LiteralPath $scriptPath -Raw
if ($probeSource -notmatch "Invoke-ProbeLogout" -or $probeSource -notmatch "Sessions/Logout") {
    throw 'Hybrid transport probe must revoke its temporary Emby session.'
}

$summary = Get-MetricSummary -Values @(10.0, 20.0, 30.0, 40.0)
if ($summary.count -ne 4 -or $summary.minMs -ne 10 -or $summary.p50Ms -ne 20 -or $summary.p95Ms -ne 40 -or $summary.maxMs -ne 40) {
    throw 'Metric summary percentiles are not deterministic.'
}

$corsSupported = Test-CorsCapability `
    -Origin 'https://app.example' `
    -GetStatus 200 `
    -GetAllowOrigin 'https://app.example' `
    -OptionsStatus 204 `
    -OptionsAllowOrigin 'https://app.example' `
    -OptionsAllowMethods 'GET, OPTIONS' `
    -OptionsAllowHeaders 'Authorization, X-Emby-Token'
if (-not $corsSupported) {
    throw 'Expected complete CORS headers to permit direct access.'
}

$corsRejected = Test-CorsCapability `
    -Origin 'https://app.example' `
    -GetStatus 200 `
    -GetAllowOrigin '' `
    -OptionsStatus 404 `
    -OptionsAllowOrigin '' `
    -OptionsAllowMethods '' `
    -OptionsAllowHeaders ''
if ($corsRejected) {
    throw 'Expected missing CORS headers and a failed preflight to reject direct access.'
}

$missingSecretsRejected = $false
$savedServer = $env:NOIRAPLAYER_QA_SERVER_URL
$savedUser = $env:NOIRAPLAYER_QA_USERNAME
$savedPassword = $env:NOIRAPLAYER_QA_PASSWORD
try {
    $env:NOIRAPLAYER_QA_SERVER_URL = $null
    $env:NOIRAPLAYER_QA_USERNAME = $null
    $env:NOIRAPLAYER_QA_PASSWORD = $null
    try {
        & $scriptPath -ServerUrl 'https://example.invalid'
    }
    catch {
        $missingSecretsRejected = $_.Exception.Message -eq 'Private probe credentials are required for benchmark mode.'
    }
}
finally {
    $env:NOIRAPLAYER_QA_SERVER_URL = $savedServer
    $env:NOIRAPLAYER_QA_USERNAME = $savedUser
    $env:NOIRAPLAYER_QA_PASSWORD = $savedPassword
}

if (-not $missingSecretsRejected) {
    throw 'Expected benchmark mode to reject missing private credentials with a sanitized error.'
}

$httpRejected = $false
try {
    & $scriptPath -ServerUrl 'http://example.invalid' -CorsOnly
}
catch {
    $httpRejected = $_.Exception.Message -eq 'Hybrid transport probes require an HTTPS server URL.'
}

if (-not $httpRejected) {
    throw 'Expected insecure server URLs to be rejected before network access.'
}

$probe = New-SanitizedProbeResult `
    -CorsResults @([pscustomobject]@{ originKind = 'packaged'; getStatus = 200; preflightStatus = 404; supported = $false }) `
    -PooledSummary $summary `
    -PerRequestSummary $summary `
    -SessionCleanup 'unsupported'
$json = $probe | ConvertTo-Json -Depth 8
$allowedTopLevel = @('schemaVersion', 'cors', 'benchmark')
$unexpectedTopLevel = @($probe.psobject.Properties.Name | Where-Object { $_ -notin $allowedTopLevel })
if ($unexpectedTopLevel.Count -ne 0) {
    throw ('Probe output contains unexpected top-level fields: ' + ($unexpectedTopLevel -join ', '))
}

if ($probe.benchmark.sessionCleanup -ne 'unsupported') {
    throw 'Probe output must distinguish an unavailable Emby logout route from a revoked session.'
}

foreach ($forbiddenName in @('serverUrl', 'username', 'password', 'accessToken', 'userId', 'itemId', 'responseBody', 'requestUrl')) {
    if ($json -match ('"' + [regex]::Escape($forbiddenName) + '"')) {
        throw ('Probe output contains forbidden identity field: ' + $forbiddenName)
    }
}

Write-Output 'noira hybrid transport probe tests ok'
