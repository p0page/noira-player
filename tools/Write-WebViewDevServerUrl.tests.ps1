$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Write-WebViewDevServerUrl.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('write-webview-dev-url-test-' + [guid]::NewGuid().ToString('N'))
$packagesRoot = Join-Path $testRoot 'Packages'
$olderPackageRoot = Join-Path $packagesRoot 'NoiraPlayer.App_oldpublisher'
$packageRoot = Join-Path $packagesRoot 'NoiraPlayer.App_testpublisher'
$summaryPath = Join-Path $testRoot 'summary.json'

New-Item -ItemType Directory -Path (Join-Path $olderPackageRoot 'LocalState') -Force | Out-Null
Start-Sleep -Milliseconds 20
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'LocalState') -Force | Out-Null

try {
    & $scriptPath `
        -Url 'http://192.168.1.20:5173/' `
        -PackagesRoot $packagesRoot `
        -SummaryPath $summaryPath

    $devUrlPath = Join-Path $packageRoot 'LocalState\webview-dev-url.txt'
    if (-not (Test-Path -LiteralPath $devUrlPath)) {
        throw 'Expected webview-dev-url.txt in the latest package LocalState.'
    }

    if ((Get-Content -LiteralPath $devUrlPath -Raw).Trim() -ne 'http://192.168.1.20:5173/') {
        throw 'Expected the exact Vite dev server URL to be written.'
    }

    if (Test-Path -LiteralPath (Join-Path $olderPackageRoot 'LocalState\webview-dev-url.txt')) {
        throw 'Expected the older package LocalState to remain unchanged.'
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($summary.action -ne 'set' -or $summary.url -ne 'http://192.168.1.20:5173/') {
        throw 'Expected the summary to report the set action and URL.'
    }

    $invalidRejected = $false
    try {
        & $scriptPath -Url 'ftp://192.168.1.20/' -PackagesRoot $packagesRoot | Out-Null
    }
    catch {
        $invalidRejected = $_.Exception.Message -like '*HTTP or HTTPS*'
    }

    if (-not $invalidRejected) {
        throw 'Expected non-HTTP dev server URLs to be rejected.'
    }

    & $scriptPath -Clear -PackagesRoot $packagesRoot -SummaryPath $summaryPath
    if (Test-Path -LiteralPath $devUrlPath) {
        throw 'Expected Clear to restore packaged WebView startup.'
    }

    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($summary.action -ne 'clear') {
        throw 'Expected the summary to report the clear action.'
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output 'write-webview-dev-server-url tests ok'
