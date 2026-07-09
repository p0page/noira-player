param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64')]
    [string]$Platform = 'x64',

    [string]$ProjectPath = '',
    [string]$MsBuildPath = '',
    [string]$ScreenshotPath = '',
    [string]$OutputPath = '',
    [int]$WaitSeconds = 20,
    [int]$PostLaunchDelaySeconds = 45,
    [int]$ScreenshotStabilizationSeconds = 2,
    [switch]$SkipBuild,
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildScriptPath = Join-Path $repoRoot 'tools\Build-NoiraModernUwp.ps1'
$registerScriptPath = Join-Path $repoRoot 'tools\Register-NoiraModernUwp.ps1'
$homePageEvidenceFileName = 'home-page-evidence.json'

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

function Remove-DevelopmentCommandFiles([string]$PackageFamilyName) {
    $localState = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState"
    if (-not (Test-Path -LiteralPath $localState)) {
        return [pscustomobject]@{
            localState = $localState
            devCommandRemoved = $false
            devCommandResultRemoved = $false
        }
    }

    $commandPath = Join-Path $localState 'dev-command.json'
    $resultPath = Join-Path $localState 'dev-command-result.txt'
    $commandExisted = Test-Path -LiteralPath $commandPath
    $resultExisted = Test-Path -LiteralPath $resultPath

    if ($commandExisted) {
        Remove-Item -LiteralPath $commandPath -Force
    }

    if ($resultExisted) {
        Remove-Item -LiteralPath $resultPath -Force
    }

    [pscustomobject]@{
        localState = $localState
        devCommandRemoved = $commandExisted
        devCommandResultRemoved = $resultExisted
    }
}

function Remove-HomePageEvidenceFile([string]$PackageFamilyName) {
    $localState = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState"
    $evidencePath = Join-Path $localState $homePageEvidenceFileName
    $existed = Test-Path -LiteralPath $evidencePath
    if ($existed) {
        Remove-Item -LiteralPath $evidencePath -Force
    }

    [pscustomobject]@{
        evidencePath = $evidencePath
        removed = $existed
    }
}

function Wait-ForNoiraProcess([int]$TimeoutSeconds) {
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        $process = Get-Process NoiraPlayer.App -ErrorAction SilentlyContinue |
            Sort-Object StartTime -Descending |
            Select-Object -First 1
        if ($null -ne $process) {
            return $process
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::Now -lt $deadline)

    throw "NoiraPlayer.App process did not start within $TimeoutSeconds seconds."
}

function Stop-NoiraAppProcess() {
    Get-Process NoiraPlayer.App -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Test-HomePageSemanticEvidenceReady([object]$Evidence) {
    if ($null -eq $Evidence) {
        return $false
    }

    if ($Evidence.page -ne 'Home') {
        return $false
    }

    if ($Evidence.renderStage -ne 'supplemental') {
        return $false
    }

    if ([int]$Evidence.libraryCount -le 0) {
        return $false
    }

    if ([int]$Evidence.rowCount -le 0 -and -not [bool]$Evidence.heroAvailable) {
        return $false
    }

    return $true
}

function Wait-ForHomePageSemanticEvidence([string]$PackageFamilyName, [int]$TimeoutSeconds) {
    $localState = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamilyName\LocalState"
    $evidencePath = Join-Path $localState $homePageEvidenceFileName
    $startedAt = [DateTimeOffset]::Now
    $deadline = $startedAt.AddSeconds($TimeoutSeconds)
    $lastStatus = 'missing'

    do {
        if (Test-Path -LiteralPath $evidencePath) {
            try {
                $evidenceFile = Get-Item -LiteralPath $evidencePath
                $rawEvidence = Get-Content -LiteralPath $evidencePath -Raw
                if (-not [string]::IsNullOrWhiteSpace($rawEvidence)) {
                    $semanticEvidence = $rawEvidence | ConvertFrom-Json
                    if (Test-HomePageSemanticEvidenceReady $semanticEvidence) {
                        return [pscustomobject]@{
                            semanticEvidenceStatus = 'ready'
                            evidencePath = $evidenceFile.FullName
                            evidenceLengthBytes = $evidenceFile.Length
                            waitedSeconds = [Math]::Round(([DateTimeOffset]::Now - $startedAt).TotalSeconds, 3)
                            semanticEvidence = $semanticEvidence
                        }
                    }

                    $lastStatus = "not-ready page=$($semanticEvidence.page) renderStage=$($semanticEvidence.renderStage) libraryCount=$($semanticEvidence.libraryCount) rowCount=$($semanticEvidence.rowCount)"
                }
            }
            catch {
                $lastStatus = "parse-failed $($_.Exception.Message)"
            }
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::Now -lt $deadline)

    throw "Modern Home semantic evidence did not reach supplemental render within $TimeoutSeconds seconds. Last status: $lastStatus. Path: $evidencePath"
}

function Save-DesktopScreenshot([string]$Path) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bounds.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Get-ScreenshotEvidence(
    [string]$Path,
    [int]$PostLaunchDelaySeconds,
    [int]$ScreenshotStabilizationSeconds,
    [object]$SemanticEvidence) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Modern page screenshot was not captured: $Path"
    }

    $screenshotFile = Get-Item -LiteralPath $Path
    if ($screenshotFile.Length -le 0) {
        throw "Modern page screenshot is empty: $Path"
    }

    [pscustomobject]@{
        captureMode = 'desktop-screenshot'
        screenshotPath = $screenshotFile.FullName
        screenshotLengthBytes = $screenshotFile.Length
        postLaunchDelaySeconds = $PostLaunchDelaySeconds
        screenshotStabilizationSeconds = $ScreenshotStabilizationSeconds
        capturedAtUtc = $screenshotFile.LastWriteTimeUtc.ToUniversalTime().ToString('O')
        semanticEvidenceStatus = $SemanticEvidence.semanticEvidenceStatus
        semanticEvidence = $SemanticEvidence
    }
}

if ($WaitSeconds -le 0) {
    throw 'WaitSeconds must be greater than zero.'
}

if ($PostLaunchDelaySeconds -le 0) {
    throw 'PostLaunchDelaySeconds must be greater than zero.'
}

if ($ScreenshotStabilizationSeconds -lt 0) {
    throw 'ScreenshotStabilizationSeconds must not be negative.'
}

if ([string]::IsNullOrWhiteSpace($ScreenshotPath)) {
    $ScreenshotPath = Join-Path $env:TEMP 'noira-modern-uwp-page.png'
}
elseif (-not [System.IO.Path]::IsPathRooted($ScreenshotPath)) {
    $ScreenshotPath = Join-Path $repoRoot $ScreenshotPath
}

if (-not $SkipBuild) {
    $buildArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $buildScriptPath,
        '-Configuration',
        $Configuration,
        '-Platform',
        $Platform,
        '-Target',
        'Publish'
    )

    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        $buildArguments += @('-ProjectPath', $ProjectPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
        $buildArguments += @('-MsBuildPath', $MsBuildPath)
    }

    Invoke-CheckedProcess 'powershell' $buildArguments
}

$registerArguments = @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $registerScriptPath,
    '-Configuration',
    $Configuration,
    '-Platform',
    $Platform,
    '-SkipBuild'
)

if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
    $registerArguments += @('-ProjectPath', $ProjectPath)
}

if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
    $registerArguments += @('-MsBuildPath', $MsBuildPath)
}

Stop-NoiraAppProcess
$registerOutput = & powershell @registerArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "Register-NoiraModernUwp.ps1 failed with exit code $LASTEXITCODE"
}

$registerReport = $registerOutput | Out-String | ConvertFrom-Json
$appUserModelId = $registerReport.appUserModelId
if ([string]::IsNullOrWhiteSpace($appUserModelId)) {
    throw 'Registered modern package did not report an appUserModelId.'
}

$developmentCommandCleanup = Remove-DevelopmentCommandFiles $registerReport.packageFamilyName
$homePageEvidenceCleanup = Remove-HomePageEvidenceFile $registerReport.packageFamilyName

$process = $null
$pageEvidence = $null
try {
    Stop-NoiraAppProcess
    Start-Process "shell:AppsFolder\$appUserModelId"
    $process = Wait-ForNoiraProcess $WaitSeconds
    $semanticEvidence = Wait-ForHomePageSemanticEvidence $registerReport.packageFamilyName $PostLaunchDelaySeconds
    if ($ScreenshotStabilizationSeconds -gt 0) {
        Start-Sleep -Seconds $ScreenshotStabilizationSeconds
    }
    Save-DesktopScreenshot $ScreenshotPath
    $pageEvidence = Get-ScreenshotEvidence $ScreenshotPath $PostLaunchDelaySeconds $ScreenshotStabilizationSeconds $semanticEvidence
}
finally {
    if (-not $KeepRunning) {
        Stop-NoiraAppProcess
    }
}

$report = [ordered]@{
    configuration = $Configuration
    platform = $Platform
    built = -not $SkipBuild
    registered = [bool]$registerReport.registered
    packageFullName = $registerReport.packageFullName
    packageFamilyName = $registerReport.packageFamilyName
    appUserModelId = $appUserModelId
    processId = $process.Id
    processStarted = $true
    screenshotPath = $pageEvidence.screenshotPath
    pageEvidence = $pageEvidence
    developmentCommandCleanup = $developmentCommandCleanup
    homePageEvidenceCleanup = $homePageEvidenceCleanup
}

$json = $report | ConvertTo-Json -Depth 6
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Write-Output $json
}
else {
    if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath = Join-Path $repoRoot $OutputPath
    }

    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }

    Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8
}
