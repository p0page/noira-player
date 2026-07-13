param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64')]
    [string]$Platform = 'x64',

    [string]$ManifestPath = '',
    [string]$CaseId = '',
    [string[]]$Purpose = @('sdr-smoke'),
    [int]$MaxTier = 1,
    [int]$DurationSeconds = 10,
    [int]$WaitSeconds = 90,
    [string]$MsBuildPath = '',
    [string]$RunPlanPath = '',
    [string]$ReportsDirectory = '',
    [string]$CommandSummaryPath = '',
    [string]$ExportSummaryPath = '',
    [string]$AnalysisSummaryPath = '',
    [string]$OutputPath = '',
    [switch]$SkipBuild,
    [switch]$RequireQualityPass,
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildScriptPath = Join-Path $repoRoot 'tools\Build-NoiraModernUwp.ps1'
$registerScriptPath = Join-Path $repoRoot 'tools\Register-NoiraModernUwp.ps1'
$writeCommandScriptPath = Join-Path $repoRoot 'tools\quality-run\Write-AppQualityRunCommand.ps1'
$exportReportsScriptPath = Join-Path $repoRoot 'tools\quality-run\Export-AppQualityRunReports.ps1'
$playbackQualityCliProjectPath = Join-Path $repoRoot 'tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj'

function Resolve-RepositoryPath([string]$Path, [string]$DefaultRelativePath) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        $Path = $DefaultRelativePath
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

function Remove-DirectoryInsideRoot([string]$Path, [string]$RootPath) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $resolvedRoot = (Resolve-Path -LiteralPath $RootPath).Path.TrimEnd('\')
    $rootPrefix = $resolvedRoot + '\'
    if (-not $resolvedPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete path outside repository root: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
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
    $processes = @(Get-Process NoiraPlayer.App -ErrorAction SilentlyContinue)
    foreach ($process in $processes) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Wait-Process -Id $process.Id -Timeout 5 -ErrorAction SilentlyContinue
    }
}

function Wait-ForFile([string]$Path, [int]$TimeoutSeconds, [string]$ResultPath) {
    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $Path) {
            try {
                Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json | Out-Null
                return [System.IO.Path]::GetFullPath($Path)
            }
            catch {
                # The app writes asynchronously; wait until the JSON is complete and readable.
            }
        }

        Start-Sleep -Seconds 2
    } while ([DateTimeOffset]::Now -lt $deadline)

    $detail = ''
    if (Test-Path -LiteralPath $ResultPath) {
        $detail = Get-Content -LiteralPath $ResultPath -Raw
    }

    throw "Quality-run report was not captured within $TimeoutSeconds seconds: $Path`n$detail"
}

if ($DurationSeconds -lt 10 -or $DurationSeconds -gt 600) {
    throw 'DurationSeconds must be between 10 and 600.'
}

if ($WaitSeconds -le $DurationSeconds) {
    throw 'WaitSeconds must be greater than DurationSeconds.'
}

$ManifestPath = Resolve-RepositoryPath $ManifestPath 'docs\qa\playback-quality-reference-manifest.example.json'
$RunPlanPath = Resolve-RepositoryPath $RunPlanPath 'docs\qa\private\modern-aot-playback-check-run-plan.local.json'
$ReportsDirectory = Resolve-RepositoryPath $ReportsDirectory 'docs\qa\private\modern-aot-playback-check-captured.local'
$CommandSummaryPath = Resolve-RepositoryPath $CommandSummaryPath 'docs\qa\private\modern-aot-playback-check-command-summary.local.json'
$ExportSummaryPath = Resolve-RepositoryPath $ExportSummaryPath 'docs\qa\private\modern-aot-playback-check-export-summary.local.json'
$AnalysisSummaryPath = Resolve-RepositoryPath $AnalysisSummaryPath 'docs\qa\private\modern-aot-playback-check-analysis.local.json'
$OutputPath = Resolve-RepositoryPath $OutputPath 'docs\qa\private\modern-aot-playback-check.local.json'

Remove-DirectoryInsideRoot $ReportsDirectory $repoRoot
foreach ($outputFile in @($RunPlanPath, $CommandSummaryPath, $ExportSummaryPath, $AnalysisSummaryPath, $OutputPath)) {
    $directory = Split-Path -Parent $outputFile
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    if (Test-Path -LiteralPath $outputFile) {
        Remove-Item -LiteralPath $outputFile -Force
    }
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

if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
    $registerArguments += @('-MsBuildPath', $MsBuildPath)
}

Stop-NoiraAppProcess

$script:playbackQualityGateSucceeded = $false
try {
$registerOutput = & powershell @registerArguments
if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "Register-NoiraModernUwp.ps1 failed with exit code $LASTEXITCODE"
}

$registerReport = $registerOutput | Out-String | ConvertFrom-Json
$appUserModelId = $registerReport.appUserModelId
if ([string]::IsNullOrWhiteSpace($appUserModelId)) {
    throw 'Registered modern package did not report an appUserModelId.'
}

$sourceRevision = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceRevision)) {
    throw 'Unable to resolve source revision for App-hosted playback evidence.'
}
$workingTreeStatus = @(& git -C $repoRoot status --porcelain --untracked-files=no)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect working tree state for App-hosted playback evidence.'
}
if ($workingTreeStatus.Count -gt 0) {
    $sourceRevision += '-dirty'
}

$planArguments = @(
    'run',
    '--project',
    $playbackQualityCliProjectPath,
    '--framework',
    'net10.0',
    '--',
    'plan-runs',
    '--manifest',
    $ManifestPath,
    '--reports-dir',
    $ReportsDirectory,
    '--duration',
    $DurationSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--source-revision',
    $sourceRevision,
    '--max-tier',
    $MaxTier.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--output',
    $RunPlanPath
)

foreach ($purposeValue in $Purpose) {
    if (-not [string]::IsNullOrWhiteSpace($purposeValue)) {
        $planArguments += @('--purpose', $purposeValue)
    }
}

Invoke-CheckedProcess 'dotnet' $planArguments

$runPlan = Get-Content -LiteralPath $RunPlanPath -Raw | ConvertFrom-Json
$cases = @($runPlan.cases)
if ($cases.Count -eq 0) {
    throw 'Playback-quality run plan did not contain any cases.'
}

if ([string]::IsNullOrWhiteSpace($CaseId)) {
    $selectedCase = $cases |
        Where-Object { $null -ne $_.devCommand } |
        Select-Object -First 1
}
else {
    $selectedCase = $cases |
        Where-Object { $_.caseId -eq $CaseId } |
        Select-Object -First 1
}

if ($null -eq $selectedCase) {
    throw 'Playback-quality run plan did not contain a runnable selected case.'
}
$expectedReportCount = 1

$writeCommandArguments = @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $writeCommandScriptPath,
    '-RunPlanPath',
    $RunPlanPath,
    '-SummaryPath',
    $CommandSummaryPath
)

if (-not [string]::IsNullOrWhiteSpace($CaseId)) {
    $writeCommandArguments += @('-CaseId', $CaseId)
}

Invoke-CheckedProcess 'powershell' $writeCommandArguments

$commandSummary = Get-Content -LiteralPath $CommandSummaryPath -Raw | ConvertFrom-Json
$localState = $commandSummary.localState
if ([string]::IsNullOrWhiteSpace($localState) -or -not (Test-Path -LiteralPath $localState)) {
    throw 'Quality-run command summary did not report an existing LocalState path.'
}

$devCommandResultPath = Join-Path $localState 'dev-command-result.txt'
if (Test-Path -LiteralPath $devCommandResultPath) {
    Remove-Item -LiteralPath $devCommandResultPath -Force
}

$reportRelativePath = $selectedCase.reportRelativePath
if ([string]::IsNullOrWhiteSpace($reportRelativePath)) {
    throw 'Selected playback-quality case did not include a reportRelativePath.'
}

$capturedRoot = Join-Path $localState 'quality-run\captured'
Remove-DirectoryInsideRoot $capturedRoot $localState
$localReportPath = Join-Path $capturedRoot ($reportRelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)

Stop-NoiraAppProcess
Start-Process explorer.exe -ArgumentList "shell:AppsFolder\$appUserModelId"
$process = Wait-ForNoiraProcess 20
$capturedReportPath = Wait-ForFile $localReportPath $WaitSeconds $devCommandResultPath

if (-not $KeepRunning) {
    Stop-NoiraAppProcess
}

Invoke-CheckedProcess 'powershell' @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $exportReportsScriptPath,
    '-OutputDirectory',
    $ReportsDirectory,
    '-SummaryPath',
    $ExportSummaryPath
)

$exportSummary = Get-Content -LiteralPath $ExportSummaryPath -Raw | ConvertFrom-Json
if ($exportSummary.exportedReportCount -ne $expectedReportCount) {
    throw "Playback-quality export count mismatch. Selected $expectedReportCount, exported $($exportSummary.exportedReportCount)."
}

Invoke-CheckedProcess 'dotnet' @(
    'run',
    '--project',
    $playbackQualityCliProjectPath,
    '--framework',
    'net10.0',
    '--',
    'analyze-report-set',
    '--reports-dir',
    $ReportsDirectory,
    '--output',
    $AnalysisSummaryPath
)

$analysisSummary = Get-Content -LiteralPath $AnalysisSummaryPath -Raw | ConvertFrom-Json
if ($analysisSummary.totalReportCount -ne $expectedReportCount) {
    throw "Playback-quality analysis count mismatch. Selected $expectedReportCount, analyzed $($analysisSummary.totalReportCount)."
}

$exportedReportPath = Join-Path $ReportsDirectory ($reportRelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $exportedReportPath)) {
    throw "Exported playback-quality report not found: $exportedReportPath"
}

$exportedReport = Get-Content -LiteralPath $exportedReportPath -Raw | ConvertFrom-Json
$modelAnalysis = $exportedReport.modelAnalysis
if ($null -eq $modelAnalysis) {
    throw 'Exported playback-quality report did not include modelAnalysis.'
}

$strictQualityFailureMessage = ''
if ($RequireQualityPass -and $modelAnalysis.result -ne 'pass') {
    $strictQualityFailureMessage = "Playback-quality report did not pass: $($modelAnalysis.result)"
}

if ($modelAnalysis.runtimeMetrics.status -ne 'captured' -or
    $modelAnalysis.runtimeMetrics.hasPlaybackSample -ne $true) {
    throw 'Playback-quality report did not include captured runtime playback samples.'
}

if ($modelAnalysis.source.status -ne 'matched') {
    throw "Playback-quality source metadata did not match: $($modelAnalysis.source.status)"
}

$failedChecks = @($modelAnalysis.failedChecks | ForEach-Object {
    [pscustomobject]@{
        signal = $_.signal
        expected = $_.expected
        actual = $_.actual
    }
})

$report = [ordered]@{
    schemaVersion = 1
    configuration = $Configuration
    platform = $Platform
    packageFullName = $registerReport.packageFullName
    packageFamilyName = $registerReport.packageFamilyName
    appUserModelId = $appUserModelId
    processId = $process.Id
    caseId = $selectedCase.caseId
    runId = $selectedCase.runId
    qualityResult = $modelAnalysis.result
    sourceStatus = $modelAnalysis.source.status
    runtimeMetricsStatus = $modelAnalysis.runtimeMetrics.status
    hasPlaybackSample = $modelAnalysis.runtimeMetrics.hasPlaybackSample
    startupDurationMs = $modelAnalysis.startup.startupDurationMs
    primaryFailureArea = $modelAnalysis.primaryFailureArea
    primaryFailureClass = $modelAnalysis.primaryFailureClass
    failedChecks = $failedChecks
    plannedCaseCount = $cases.Count
    selectedCaseCount = $expectedReportCount
    exportedReportCount = $exportSummary.exportedReportCount
    analyzedReportCount = $analysisSummary.totalReportCount
    reportRelativePath = $reportRelativePath
    capturedReportPath = $capturedReportPath
    exportedReportPath = (Resolve-Path -LiteralPath $exportedReportPath).Path
    runPlanPath = (Resolve-Path -LiteralPath $RunPlanPath).Path
    commandSummaryPath = (Resolve-Path -LiteralPath $CommandSummaryPath).Path
    exportSummaryPath = (Resolve-Path -LiteralPath $ExportSummaryPath).Path
    analysisSummaryPath = (Resolve-Path -LiteralPath $AnalysisSummaryPath).Path
}

$json = $report | ConvertTo-Json -Depth 8
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Write-Output $json
}
else {
    Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8
}

if (-not [string]::IsNullOrWhiteSpace($strictQualityFailureMessage)) {
    throw $strictQualityFailureMessage
}

$script:playbackQualityGateSucceeded = $true
}
finally {
    if (-not $KeepRunning -or -not $script:playbackQualityGateSucceeded) {
        Stop-NoiraAppProcess
    }
}
