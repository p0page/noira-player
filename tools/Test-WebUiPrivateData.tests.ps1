$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Test-WebUiPrivateData.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('noira-web-ui-private-data-' + [Guid]::NewGuid().ToString('N'))
$privateValues = @(
    'https://fake-emby.invalid:18443',
    'fake-noira-user-7d31',
    'fake-noira-password-93f8'
)

function Invoke-Git([string]$RepositoryRoot, [string[]]$Arguments) {
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & git -C $RepositoryRoot @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw ('git failed: ' + ($output -join "`n"))
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function New-FakeRepository([string]$Name) {
    $repositoryRoot = Join-Path $tempRoot $Name
    New-Item -ItemType Directory -Path $repositoryRoot -Force | Out-Null
    Invoke-Git $repositoryRoot @('init', '--quiet')
    Invoke-Git $repositoryRoot @('config', 'user.email', 'test@example.invalid')
    Invoke-Git $repositoryRoot @('config', 'user.name', 'Noira Test')

    @'
.private/
src/NoiraPlayer.Web/dist/
docs/qa/private/
docs/qa/*.local.json
'@ | Set-Content -LiteralPath (Join-Path $repositoryRoot '.gitignore') -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $repositoryRoot 'README.md') -Value 'public fixture' -Encoding UTF8
    Invoke-Git $repositoryRoot @('add', '.gitignore', 'README.md')

    $privateDirectory = Join-Path $repositoryRoot '.private'
    New-Item -ItemType Directory -Path $privateDirectory -Force | Out-Null
    $privateEnvPath = Join-Path $privateDirectory 'emby-test.local.env'
    @"
NOIRA_EMBY_SERVER=$($privateValues[0])
NOIRA_EMBY_USER=$($privateValues[1])
NOIRA_EMBY_PASSWORD=$($privateValues[2])
"@ | Set-Content -LiteralPath $privateEnvPath -Encoding UTF8

    return [pscustomobject]@{
        RepositoryRoot = $repositoryRoot
        PrivateEnvPath = $privateEnvPath
    }
}

function Invoke-Guard([object]$Fixture, [string]$PrivateEnvPath = $Fixture.PrivateEnvPath) {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath `
        -RepositoryRoot $Fixture.RepositoryRoot `
        -PrivateEnvPath $PrivateEnvPath 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Text = ($output -join "`n")
    }
}

function Invoke-GuardWithDefaults([object]$Fixture) {
    $toolsPath = Join-Path $Fixture.RepositoryRoot 'tools'
    New-Item -ItemType Directory -Path $toolsPath -Force | Out-Null
    $copiedScriptPath = Join-Path $toolsPath 'Test-WebUiPrivateData.ps1'
    Copy-Item -LiteralPath $scriptPath -Destination $copiedScriptPath
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $copiedScriptPath 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Text = ($output -join "`n")
    }
}

function Assert-NoPrivateOutput([string]$Text) {
    foreach ($value in $privateValues) {
        if ($Text.Contains($value)) {
            throw 'Guard output exposed a fake private value.'
        }
    }
}

function Assert-Result([object]$Result, [int]$ExitCode, [string]$Category) {
    Assert-NoPrivateOutput $Result.Text
    foreach ($line in @($Result.Text -split "`r?`n" | Where-Object { $_.Length -gt 0 })) {
        if ($line -notmatch '^result=(pass|fail|skip|scan) category=[a-z-]+ count=[0-9]+$') {
            throw ("Guard emitted unsafe or unexpected output:`n" + $Result.Text)
        }
    }
    if ($Result.ExitCode -ne $ExitCode) {
        throw ("Expected exit code $ExitCode for $Category. Output:`n" + $Result.Text)
    }
    if ($Result.Text -notmatch [regex]::Escape($Category)) {
        throw ("Expected output category '$Category'. Output:`n" + $Result.Text)
    }
}

try {
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw 'Test-WebUiPrivateData.ps1 must exist.'
    }

    $envOnly = New-FakeRepository 'env-only'
    Assert-Result (Invoke-Guard $envOnly) 0 'result=pass'

    $defaults = New-FakeRepository 'defaults'
    Assert-Result (Invoke-GuardWithDefaults $defaults) 0 'result=pass'

    $missingEnv = New-FakeRepository 'missing-env'
    Remove-Item -LiteralPath $missingEnv.PrivateEnvPath -Force
    Assert-Result (Invoke-Guard $missingEnv) 0 'result=skip'

    $tracked = New-FakeRepository 'tracked'
    Set-Content -LiteralPath (Join-Path $tracked.RepositoryRoot 'tracked.txt') -Value $privateValues[2] -Encoding UTF8
    Invoke-Git $tracked.RepositoryRoot @('add', 'tracked.txt')
    $trackedResult = Invoke-Guard $tracked
    Assert-Result $trackedResult 1 'category=tracked'
    if ($trackedResult.Text -notmatch 'count=1') {
        throw 'Expected one tracked finding.'
    }

    $dist = New-FakeRepository 'dist'
    $distPath = Join-Path $dist.RepositoryRoot 'src\NoiraPlayer.Web\dist\assets'
    New-Item -ItemType Directory -Path $distPath -Force | Out-Null
    [IO.File]::WriteAllBytes(
        (Join-Path $distPath 'bundle.bin'),
        [Text.Encoding]::UTF8.GetBytes("binary-prefix`0$($privateValues[1])`0binary-suffix")
    )
    Assert-Result (Invoke-Guard $dist) 1 'category=dist'

    $qa = New-FakeRepository 'qa'
    $qaPath = Join-Path $qa.RepositoryRoot 'docs\qa'
    New-Item -ItemType Directory -Path $qaPath -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $qaPath 'acceptance.md') -Value $privateValues[0] -Encoding UTF8
    Assert-Result (Invoke-Guard $qa) 1 'category=qa'

    $ignoredQa = New-FakeRepository 'ignored-qa'
    $ignoredQaPath = Join-Path $ignoredQa.RepositoryRoot 'docs\qa'
    New-Item -ItemType Directory -Path $ignoredQaPath -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $ignoredQaPath 'evidence.local.json') -Value $privateValues[0] -Encoding UTF8
    Assert-Result (Invoke-Guard $ignoredQa) 0 'result=pass'

    $readError = New-FakeRepository 'read-error'
    $unavailablePath = Join-Path $readError.RepositoryRoot 'unavailable.txt'
    Set-Content -LiteralPath $unavailablePath -Value 'public fixture' -Encoding UTF8
    Invoke-Git $readError.RepositoryRoot @('add', 'unavailable.txt')
    Remove-Item -LiteralPath $unavailablePath -Force
    Assert-Result (Invoke-Guard $readError) 1 'category=read-error'

    $envReadError = New-FakeRepository 'env-read-error'
    Assert-Result (Invoke-Guard $envReadError $envReadError.RepositoryRoot) 1 'category=environment-error'

    Write-Output 'web UI private data tests ok'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
