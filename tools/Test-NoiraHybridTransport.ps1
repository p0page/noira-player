param(
    [string]$ServerUrl = $env:NOIRAPLAYER_QA_SERVER_URL,
    [System.Management.Automation.PSCredential]$Credential,
    [int]$SampleCount = 12,
    [string]$OutputPath = '',
    [switch]$CorsOnly,
    [switch]$AllowHttp,
    [switch]$LoadFunctionsOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

function Get-Percentile {
    param(
        [double[]]$Values,
        [ValidateRange(0, 100)]
        [double]$Percentile
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        throw 'At least one metric value is required.'
    }

    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1)
    return [double]$sorted[$index]
}

function Get-MetricSummary {
    param([double[]]$Values)

    if ($null -eq $Values -or $Values.Count -eq 0) {
        throw 'At least one metric value is required.'
    }

    return [pscustomobject][ordered]@{
        count = $Values.Count
        minMs = [Math]::Round(($Values | Measure-Object -Minimum).Minimum, 3)
        p50Ms = [Math]::Round((Get-Percentile -Values $Values -Percentile 50), 3)
        p95Ms = [Math]::Round((Get-Percentile -Values $Values -Percentile 95), 3)
        maxMs = [Math]::Round(($Values | Measure-Object -Maximum).Maximum, 3)
    }
}

function Test-CorsCapability {
    param(
        [string]$Origin,
        [int]$GetStatus,
        [string]$GetAllowOrigin,
        [int]$OptionsStatus,
        [string]$OptionsAllowOrigin,
        [string]$OptionsAllowMethods,
        [string]$OptionsAllowHeaders
    )

    $getOriginAllowed = $GetAllowOrigin -eq '*' -or
        [string]::Equals($GetAllowOrigin, $Origin, [System.StringComparison]::OrdinalIgnoreCase)
    $optionsOriginAllowed = $OptionsAllowOrigin -eq '*' -or
        [string]::Equals($OptionsAllowOrigin, $Origin, [System.StringComparison]::OrdinalIgnoreCase)
    $methods = @(($OptionsAllowMethods -split ',') | ForEach-Object { $_.Trim().ToLowerInvariant() })
    $headers = @(($OptionsAllowHeaders -split ',') | ForEach-Object { $_.Trim().ToLowerInvariant() })

    return $GetStatus -ge 200 -and $GetStatus -lt 300 -and
        $getOriginAllowed -and
        $OptionsStatus -ge 200 -and $OptionsStatus -lt 300 -and
        $optionsOriginAllowed -and
        $methods -contains 'get' -and
        $headers -contains 'authorization' -and
        $headers -contains 'x-emby-token'
}

function New-SanitizedProbeResult {
    param(
        [object[]]$CorsResults,
        [object]$PooledSummary,
        [object]$PerRequestSummary,
        [ValidateSet('not-run', 'revoked', 'unsupported')]
        [string]$SessionCleanup = 'not-run'
    )

    $benchmark = $null
    if ($null -ne $PooledSummary -and $null -ne $PerRequestSummary) {
        $p50Ratio = if ($PerRequestSummary.p50Ms -gt 0) {
            [Math]::Round($PooledSummary.p50Ms / $PerRequestSummary.p50Ms, 4)
        }
        else {
            0
        }
        $p95Ratio = if ($PerRequestSummary.p95Ms -gt 0) {
            [Math]::Round($PooledSummary.p95Ms / $PerRequestSummary.p95Ms, 4)
        }
        else {
            0
        }
        $benchmark = [pscustomobject][ordered]@{
            sampleCount = $PooledSummary.count
            sessionCleanup = $SessionCleanup
            pooled = $PooledSummary
            perRequest = $PerRequestSummary
            ratios = [pscustomobject][ordered]@{
                p50 = $p50Ratio
                p95 = $p95Ratio
            }
            gates = [pscustomobject][ordered]@{
                p50NonRegression = $p50Ratio -le 1.10
                p95NonRegression = $p95Ratio -le 1.15
                desiredP50Improvement = $p50Ratio -le 0.85
            }
        }
    }

    return [pscustomobject][ordered]@{
        schemaVersion = 'noira.hybrid-transport-probe.v1'
        cors = @($CorsResults)
        benchmark = $benchmark
    }
}

function New-ProbeHttpClient {
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.AllowAutoRedirect = $false
    $handler.UseCookies = $false
    $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::GZip -bor
        [System.Net.DecompressionMethods]::Deflate
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(12)
    return $client
}

function Get-ResponseHeaderValue {
    param(
        [System.Net.Http.HttpResponseMessage]$Response,
        [string]$Name
    )

    try {
        return [string]::Join(',', $Response.Headers.GetValues($Name))
    }
    catch {
        try {
            return [string]::Join(',', $Response.Content.Headers.GetValues($Name))
        }
        catch {
            return ''
        }
    }
}

function Invoke-CorsRequestPair {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$PublicInfoUrl,
        [string]$OriginKind,
        [string]$Origin
    )

    $getRequest = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Get, $PublicInfoUrl)
    [void]$getRequest.Headers.TryAddWithoutValidation('Origin', $Origin)
    $getResponse = $null
    try {
        $getResponse = $Client.SendAsync($getRequest).GetAwaiter().GetResult()
        [void]$getResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $getStatus = [int]$getResponse.StatusCode
        $getAllowOrigin = Get-ResponseHeaderValue -Response $getResponse -Name 'Access-Control-Allow-Origin'
    }
    finally {
        if ($null -ne $getResponse) { $getResponse.Dispose() }
        $getRequest.Dispose()
    }

    $optionsMethod = New-Object System.Net.Http.HttpMethod('OPTIONS')
    $optionsRequest = New-Object System.Net.Http.HttpRequestMessage($optionsMethod, $PublicInfoUrl)
    [void]$optionsRequest.Headers.TryAddWithoutValidation('Origin', $Origin)
    [void]$optionsRequest.Headers.TryAddWithoutValidation('Access-Control-Request-Method', 'GET')
    [void]$optionsRequest.Headers.TryAddWithoutValidation(
        'Access-Control-Request-Headers',
        'authorization,x-emby-token')
    $optionsResponse = $null
    try {
        $optionsResponse = $Client.SendAsync($optionsRequest).GetAwaiter().GetResult()
        [void]$optionsResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $optionsStatus = [int]$optionsResponse.StatusCode
        $optionsAllowOrigin = Get-ResponseHeaderValue -Response $optionsResponse -Name 'Access-Control-Allow-Origin'
        $optionsAllowMethods = Get-ResponseHeaderValue -Response $optionsResponse -Name 'Access-Control-Allow-Methods'
        $optionsAllowHeaders = Get-ResponseHeaderValue -Response $optionsResponse -Name 'Access-Control-Allow-Headers'
    }
    finally {
        if ($null -ne $optionsResponse) { $optionsResponse.Dispose() }
        $optionsRequest.Dispose()
    }

    return [pscustomobject][ordered]@{
        originKind = $OriginKind
        getStatus = $getStatus
        preflightStatus = $optionsStatus
        supported = Test-CorsCapability `
            -Origin $Origin `
            -GetStatus $getStatus `
            -GetAllowOrigin $getAllowOrigin `
            -OptionsStatus $optionsStatus `
            -OptionsAllowOrigin $optionsAllowOrigin `
            -OptionsAllowMethods $optionsAllowMethods `
            -OptionsAllowHeaders $optionsAllowHeaders
    }
}

function Invoke-ProbeAuthentication {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$AuthenticationUrl,
        [string]$Username,
        [string]$PlaintextPassword
    )

    $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, $AuthenticationUrl)
    [void]$request.Headers.TryAddWithoutValidation(
        'Authorization',
        'Emby Client="Noira", Device="Windows", DeviceId="noira-hybrid-transport-probe", Version="0.1.0"')
    $request.Content = New-Object System.Net.Http.StringContent(
        (@{ Username = $Username; Pw = $PlaintextPassword } | ConvertTo-Json -Compress),
        [System.Text.Encoding]::UTF8,
        'application/json')
    $response = $null
    try {
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw 'Authentication returned a non-success status.'
        }

        $auth = $body | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace([string]$auth.AccessToken) -or
            [string]::IsNullOrWhiteSpace([string]$auth.User.Id)) {
            throw 'Authentication response was incomplete.'
        }

        return [pscustomobject]@{
            userId = [string]$auth.User.Id
            accessToken = [string]$auth.AccessToken
        }
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
    }
}

function Invoke-ProbeLogout {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$LogoutUrl,
        [string]$Authorization,
        [string]$AccessToken
    )

    $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, $LogoutUrl)
    [void]$request.Headers.TryAddWithoutValidation('Authorization', $Authorization)
    [void]$request.Headers.TryAddWithoutValidation('X-Emby-Token', $AccessToken)
    $response = $null
    try {
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        [void]$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [int]$response.StatusCode
    }
    catch {
        return 0
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
    }
}

function Invoke-TimedMetadataGet {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$RequestUrl,
        [string]$Authorization,
        [string]$AccessToken
    )

    $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Get, $RequestUrl)
    $request.Headers.Accept.Add((New-Object System.Net.Http.Headers.MediaTypeWithQualityHeaderValue('application/json')))
    [void]$request.Headers.TryAddWithoutValidation('Authorization', $Authorization)
    [void]$request.Headers.TryAddWithoutValidation('X-Emby-Token', $AccessToken)
    $response = $null
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = $Client.SendAsync(
            $request,
            [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $stopwatch.Stop()
        if (-not $response.IsSuccessStatusCode -or [string]::IsNullOrWhiteSpace($body)) {
            throw 'Metadata request did not return a successful non-empty response.'
        }

        return [pscustomobject]@{
            status = [int]$response.StatusCode
            elapsedMs = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            bodyBytes = [System.Text.Encoding]::UTF8.GetByteCount($body)
        }
    }
    finally {
        $stopwatch.Stop()
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
    }
}

function Invoke-PerRequestMetadataGet {
    param(
        [string]$RequestUrl,
        [string]$Authorization,
        [string]$AccessToken
    )

    $client = New-ProbeHttpClient
    try {
        return Invoke-TimedMetadataGet `
            -Client $client `
            -RequestUrl $RequestUrl `
            -Authorization $Authorization `
            -AccessToken $AccessToken
    }
    finally {
        $client.Dispose()
    }
}

if ($LoadFunctionsOnly) {
    return
}

$serverUri = $null
if ([string]::IsNullOrWhiteSpace($ServerUrl) -or
    -not [uri]::TryCreate($ServerUrl.Trim(), [System.UriKind]::Absolute, [ref]$serverUri)) {
    throw 'A valid absolute server URL is required.'
}

if (-not $AllowHttp -and $serverUri.Scheme -ne [uri]::UriSchemeHttps) {
    throw 'Hybrid transport probes require an HTTPS server URL.'
}

if ($serverUri.Scheme -ne [uri]::UriSchemeHttps -and $serverUri.Scheme -ne [uri]::UriSchemeHttp) {
    throw 'Hybrid transport probes require an HTTP or HTTPS server URL.'
}

if ($SampleCount -lt 12) {
    throw 'SampleCount must be at least 12.'
}

if (-not [string]::IsNullOrWhiteSpace($OutputPath) -and -not $OutputPath.EndsWith('.local.json')) {
    throw 'Probe output files must use the ignored .local.json suffix.'
}

$effectiveUsername = ''
$plaintextPassword = $null
if (-not $CorsOnly) {
    if ($null -ne $Credential) {
        $effectiveUsername = $Credential.UserName
        $plaintextPassword = $Credential.GetNetworkCredential().Password
    }
    else {
        $effectiveUsername = [string]$env:NOIRAPLAYER_QA_USERNAME
        $plaintextPassword = [string]$env:NOIRAPLAYER_QA_PASSWORD
    }

    if ([string]::IsNullOrWhiteSpace($effectiveUsername) -or
        [string]::IsNullOrWhiteSpace($plaintextPassword)) {
        $plaintextPassword = $null
        throw 'Private probe credentials are required for benchmark mode.'
    }
}

$stage = 'initialization'
$sharedClient = $null
try {
    $baseUrl = $serverUri.AbsoluteUri.TrimEnd('/')
    $publicInfoUrl = $baseUrl + '/System/Info/Public'
    $sharedClient = New-ProbeHttpClient
    $stage = 'CORS capability probe'
    $corsResults = @(
        Invoke-CorsRequestPair `
            -Client $sharedClient `
            -PublicInfoUrl $publicInfoUrl `
            -OriginKind 'packaged' `
            -Origin 'https://app.noira.local'
        Invoke-CorsRequestPair `
            -Client $sharedClient `
            -PublicInfoUrl $publicInfoUrl `
            -OriginKind 'vite-loopback' `
            -Origin 'http://127.0.0.1:5173'
    )

    $pooledSummary = $null
    $perRequestSummary = $null
    $sessionCleanup = 'not-run'
    if (-not $CorsOnly) {
        $stage = 'authentication'
        $auth = Invoke-ProbeAuthentication `
            -Client $sharedClient `
            -AuthenticationUrl ($baseUrl + '/Users/AuthenticateByName') `
            -Username $effectiveUsername `
            -PlaintextPassword $plaintextPassword
        $escapedUserId = [uri]::EscapeDataString($auth.userId)
        $metadataUrl = $baseUrl + '/Users/' + $escapedUserId + '/Views?Fields=PrimaryImageAspectRatio,ImageTags'
        $authorization = 'Emby UserId="' + $auth.userId + '", Client="Noira", Device="Windows", DeviceId="noira-hybrid-transport-probe", Version="0.1.0"'
        $logoutStatus = 0
        try {
            $stage = 'benchmark warm-up'
            [void](Invoke-TimedMetadataGet `
                -Client $sharedClient `
                -RequestUrl $metadataUrl `
                -Authorization $authorization `
                -AccessToken $auth.accessToken)
            [void](Invoke-PerRequestMetadataGet `
                -RequestUrl $metadataUrl `
                -Authorization $authorization `
                -AccessToken $auth.accessToken)

            $stage = 'benchmark sampling'
            $pooledValues = New-Object 'System.Collections.Generic.List[double]'
            $perRequestValues = New-Object 'System.Collections.Generic.List[double]'
            for ($sampleIndex = 0; $sampleIndex -lt $SampleCount; $sampleIndex++) {
                if (($sampleIndex % 2) -eq 0) {
                    $pooled = Invoke-TimedMetadataGet -Client $sharedClient -RequestUrl $metadataUrl -Authorization $authorization -AccessToken $auth.accessToken
                    $perRequest = Invoke-PerRequestMetadataGet -RequestUrl $metadataUrl -Authorization $authorization -AccessToken $auth.accessToken
                }
                else {
                    $perRequest = Invoke-PerRequestMetadataGet -RequestUrl $metadataUrl -Authorization $authorization -AccessToken $auth.accessToken
                    $pooled = Invoke-TimedMetadataGet -Client $sharedClient -RequestUrl $metadataUrl -Authorization $authorization -AccessToken $auth.accessToken
                }

                [void]$pooledValues.Add([double]$pooled.elapsedMs)
                [void]$perRequestValues.Add([double]$perRequest.elapsedMs)
            }

            $pooledSummary = Get-MetricSummary -Values @($pooledValues)
            $perRequestSummary = Get-MetricSummary -Values @($perRequestValues)
        }
        finally {
            $stage = 'session logout'
            $logoutStatus = Invoke-ProbeLogout `
                -Client $sharedClient `
                -LogoutUrl ($baseUrl + '/Sessions/Logout') `
                -Authorization $authorization `
                -AccessToken $auth.accessToken
            $authorization = ''
            $auth.accessToken = ''
            $auth.userId = ''
        }

        if ($logoutStatus -ge 200 -and $logoutStatus -lt 300) {
            $sessionCleanup = 'revoked'
        }
        elseif ($logoutStatus -eq 404) {
            $sessionCleanup = 'unsupported'
        }
        else {
            $stage = 'session logout status ' + $logoutStatus.ToString()
            throw 'The temporary probe session could not be revoked.'
        }
    }

    $stage = 'output serialization'
    $result = New-SanitizedProbeResult `
        -CorsResults $corsResults `
        -PooledSummary $pooledSummary `
        -PerRequestSummary $perRequestSummary `
        -SessionCleanup $sessionCleanup
    $resultJson = $result | ConvertTo-Json -Depth 8
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        Write-Output $resultJson
    }
    else {
        $outputDirectory = Split-Path -Parent $OutputPath
        if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
            New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
        }

        Set-Content -LiteralPath $OutputPath -Value $resultJson -Encoding UTF8
        Write-Output 'Hybrid transport probe completed; sanitized output was written to the requested local file.'
    }
}
catch {
    $exceptionType = $_.Exception.GetType().Name
    throw ('Hybrid transport probe failed during ' + $stage + ' (' + $exceptionType + ').')
}
finally {
    if ($null -ne $sharedClient) {
        $sharedClient.Dispose()
    }

    $plaintextPassword = $null
    $effectiveUsername = ''
    $env:NOIRAPLAYER_QA_SERVER_URL = $null
    $env:NOIRAPLAYER_QA_USERNAME = $null
    $env:NOIRAPLAYER_QA_PASSWORD = $null
}
