param(
    [ValidateSet('Build', 'Publish', 'Verify', 'Check', 'PlaybackCheck', 'CutoverCheck')]
    [string]$Target = 'Build',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64')]
    [string]$Platform = 'x64',

    [string]$MsBuildPath = '',
    [string]$ScreenshotPath = '',
    [string]$OutputPath = '',
    [int]$PostLaunchDelaySeconds = 45,
    [switch]$RequirePlaybackQualityPass
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modernSolutionPath = Join-Path $repoRoot 'NoiraPlayer.sln'
$modernBuildScriptPath = Join-Path $repoRoot 'tools\Build-NoiraModernUwp.ps1'
$modernVerificationScriptPath = Join-Path $repoRoot 'tools\Test-NoiraModernUwp.ps1'
$modernPlaybackQualityScriptPath = Join-Path $repoRoot 'tools\Test-NoiraModernPlaybackQuality.ps1'
$modernToolchainScriptPath = Join-Path $PSScriptRoot 'NoiraModernToolchain.ps1'
$coreTestsProjectPath = Join-Path $repoRoot 'tests\NoiraPlayer.Core.Tests\NoiraPlayer.Core.Tests.csproj'
$CutoverPlaybackQualityMaxAttempts = 3

. $modernToolchainScriptPath

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

function Resolve-OutputPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Invoke-BuildEntryPoint([string[]]$Arguments) {
    Invoke-CheckedProcess 'powershell' (@(
            '-NoProfile',
            '-ExecutionPolicy',
            'Bypass',
            '-File',
            $PSCommandPath
        ) + $Arguments)
}

function Add-OptionalMsBuildPathArgument([string[]]$Arguments) {
    if ([string]::IsNullOrWhiteSpace($MsBuildPath)) {
        return $Arguments
    }

    return $Arguments + @('-MsBuildPath', $MsBuildPath)
}

function New-MigrationArtifactSet([string]$SummaryOutputPath) {
    $timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $summaryPath = Resolve-OutputPath $SummaryOutputPath
    $artifactRoot = if ([string]::IsNullOrWhiteSpace($summaryPath)) {
        Join-Path $env:TEMP "noira-migration-check-$timestamp"
    }
    else {
        $directory = Split-Path -Parent $summaryPath
        if ([string]::IsNullOrWhiteSpace($directory)) {
            $repoRoot
        }
        else {
            $directory
        }
    }

    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

    [pscustomobject]@{
        root = $artifactRoot
        summaryPath = $summaryPath
        timestamp = $timestamp
    }
}

function New-MigrationArtifactPath([object]$Artifacts, [string]$Name, [string]$Extension) {
    if ($Extension -eq '.png') {
        return Join-Path $env:TEMP "migration-check-$($Artifacts.timestamp)-$Name$Extension"
    }

    Join-Path $Artifacts.root "migration-check-$($Artifacts.timestamp)-$Name$Extension"
}

function Read-MigrationChildReport([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        throw "Migration child report not found: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function New-HomePageGateSummary([string]$OutputPath) {
    $report = Read-MigrationChildReport $OutputPath
    $pageEvidence = $report.pageEvidence
    $semanticContainer = $pageEvidence.semanticEvidence
    $semanticEvidence = $semanticContainer.semanticEvidence

    if ($null -eq $pageEvidence -or $null -eq $semanticContainer -or $null -eq $semanticEvidence) {
        throw "Modern page gate report is missing Home semantic evidence: $OutputPath"
    }

    [ordered]@{
        semanticEvidenceStatus = $pageEvidence.semanticEvidenceStatus
        renderStage = $semanticEvidence.renderStage
        libraryCount = $semanticEvidence.libraryCount
        libraryWithIdCount = $semanticEvidence.libraryWithIdCount
        libraryPreviewCount = $semanticEvidence.libraryPreviewCount
        libraryPreviewMissingCount = $semanticEvidence.libraryPreviewMissingCount
        previewEvidenceStatus = $semanticEvidence.previewEvidenceStatus
        homeSectionCount = $semanticEvidence.homeSectionCount
        rowCount = $semanticEvidence.rowCount
        continueItemCount = $semanticEvidence.continueItemCount
        nextUpItemCount = $semanticEvidence.nextUpItemCount
        latestItemCount = $semanticEvidence.latestItemCount
        heroAvailable = $semanticEvidence.heroAvailable
        interactiveRequestMaxAttempts = $semanticEvidence.interactiveRequestMaxAttempts
        requiredInteractiveRequestMaxAttempts = $semanticEvidence.requiredInteractiveRequestMaxAttempts
        waitedSeconds = $semanticContainer.waitedSeconds
        postLaunchDelaySeconds = $pageEvidence.postLaunchDelaySeconds
        screenshotStabilizationSeconds = $pageEvidence.screenshotStabilizationSeconds
        screenshotLengthBytes = $pageEvidence.screenshotLengthBytes
    }
}

function New-PlaybackGateSummary([string]$OutputPath) {
    $report = Read-MigrationChildReport $OutputPath

    [ordered]@{
        caseId = $report.caseId
        runId = $report.runId
        qualityResult = $report.qualityResult
        sourceStatus = $report.sourceStatus
        runtimeMetricsStatus = $report.runtimeMetricsStatus
        hasPlaybackSample = $report.hasPlaybackSample
        startupDurationMs = $report.startupDurationMs
        primaryFailureArea = $report.primaryFailureArea
        primaryFailureClass = $report.primaryFailureClass
        plannedCaseCount = $report.plannedCaseCount
        exportedReportCount = $report.exportedReportCount
        analyzedReportCount = $report.analyzedReportCount
        failedChecks = @($report.failedChecks)
    }
}

function Invoke-CutoverPlaybackCheck([object]$Artifacts) {
    $attemptErrors = [System.Collections.Generic.List[string]]::new()

    for ($attempt = 1; $attempt -le $CutoverPlaybackQualityMaxAttempts; $attempt++) {
        $artifactName = if ($attempt -eq 1) {
            'modern-cutover-playback-check'
        }
        else {
            "modern-cutover-playback-check-attempt-$attempt"
        }

        $playbackOutput = New-MigrationArtifactPath $Artifacts $artifactName '.local.json'
        try {
            Invoke-BuildEntryPoint (Add-OptionalMsBuildPathArgument @(
                '-Target',
                'PlaybackCheck',
                '-Configuration',
                'Debug',
                '-Platform',
                $Platform,
                '-OutputPath',
                $playbackOutput,
                '-RequirePlaybackQualityPass'
            ))

            return [pscustomobject]@{
                outputPath = $playbackOutput
                attemptCount = $attempt
                attemptErrors = @($attemptErrors)
            }
        }
        catch {
            $attemptErrors.Add($_.Exception.Message)
            if ($attempt -ge $CutoverPlaybackQualityMaxAttempts) {
                throw
            }

            Write-Warning "Strict playback-quality cutover attempt $attempt failed. Retrying to filter startup-only smoke variance. $($_.Exception.Message)"
        }
    }

    throw 'Strict playback-quality cutover retry loop exited unexpectedly.'
}

function Invoke-CutoverCheck() {
    $startedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $artifacts = New-MigrationArtifactSet $OutputPath
    $gates = [System.Collections.Generic.List[object]]::new()

    $debugOutput = New-MigrationArtifactPath $artifacts 'modern-cutover-debug-check' '.local.json'
    $debugScreenshot = New-MigrationArtifactPath $artifacts 'modern-cutover-debug-check' '.png'
    Invoke-BuildEntryPoint (Add-OptionalMsBuildPathArgument @(
        '-Target',
        'Check',
        '-Configuration',
        'Debug',
        '-Platform',
        $Platform,
        '-PostLaunchDelaySeconds',
        $PostLaunchDelaySeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        '-ScreenshotPath',
        $debugScreenshot,
        '-OutputPath',
        $debugOutput
    ))
    $debugHomePageEvidence = New-HomePageGateSummary $debugOutput
    $gates.Add([ordered]@{
            name = 'modern-cutover-debug-check'
            target = 'Check'
            configuration = 'Debug'
            outputPath = $debugOutput
            screenshotPath = $debugScreenshot
            homePageEvidence = $debugHomePageEvidence
        })

    $releaseOutput = New-MigrationArtifactPath $artifacts 'modern-cutover-release-check' '.local.json'
    $releaseScreenshot = New-MigrationArtifactPath $artifacts 'modern-cutover-release-check' '.png'
    Invoke-BuildEntryPoint (Add-OptionalMsBuildPathArgument @(
        '-Target',
        'Check',
        '-Configuration',
        'Release',
        '-Platform',
        $Platform,
        '-PostLaunchDelaySeconds',
        $PostLaunchDelaySeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        '-ScreenshotPath',
        $releaseScreenshot,
        '-OutputPath',
        $releaseOutput
    ))
    $releaseHomePageEvidence = New-HomePageGateSummary $releaseOutput
    $gates.Add([ordered]@{
            name = 'modern-cutover-release-check'
            target = 'Check'
            configuration = 'Release'
            outputPath = $releaseOutput
            screenshotPath = $releaseScreenshot
            homePageEvidence = $releaseHomePageEvidence
        })

    $playbackResult = Invoke-CutoverPlaybackCheck $artifacts
    $playbackOutput = $playbackResult.outputPath
    $playbackEvidence = New-PlaybackGateSummary $playbackOutput
    $gates.Add([ordered]@{
            name = 'modern-cutover-playback-check'
            target = 'PlaybackCheck'
            configuration = 'Debug'
            outputPath = $playbackOutput
            playbackAttemptCount = $playbackResult.attemptCount
            playbackAttemptErrors = $playbackResult.attemptErrors
            requirePlaybackQualityPass = $true
            playbackQualityGatePolicy = 'strict-pass-required'
            playbackEvidence = $playbackEvidence
        })

    $report = [ordered]@{
        cutoverCheckSucceeded = $true
        startedAtUtc = $startedAtUtc
        completedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        platform = $Platform
        postLaunchDelaySeconds = $PostLaunchDelaySeconds
        artifactRoot = $artifacts.root
        modernStandalone = $true
        legacyValidationIncluded = $false
        requirePlaybackQualityPass = $true
        playbackQualityGatePolicy = 'strict-pass-required'
        playbackAttemptCount = $playbackResult.attemptCount
        playbackAttemptErrors = $playbackResult.attemptErrors
        homePageEvidence = [ordered]@{
            debug = $debugHomePageEvidence
            release = $releaseHomePageEvidence
        }
        playbackEvidence = $playbackEvidence
        strictPlaybackQualityResult = $playbackEvidence.qualityResult
        strictPlaybackQualityFailedChecks = $playbackEvidence.failedChecks
        gates = $gates
    }

    $json = $report | ConvertTo-Json -Depth 8
    if ([string]::IsNullOrWhiteSpace($artifacts.summaryPath)) {
        Write-Output $json
    }
    else {
        Set-Content -LiteralPath $artifacts.summaryPath -Value $json -Encoding UTF8
    }
}

function Invoke-SolutionBuild([string]$MsBuild, [string]$SolutionPath, [string[]]$AdditionalProperties = @()) {
    $arguments = @(
        $SolutionPath,
        '/restore',
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        '/p:AppxBundle=Never',
        '/p:AppxPackageSigningEnabled=false',
        '/m',
        '/v:minimal'
    ) + $AdditionalProperties

    Invoke-CheckedProcess $MsBuild $arguments
}

function Invoke-CoreTests() {
    Invoke-CheckedProcess 'dotnet' @(
        'test',
        $coreTestsProjectPath,
        '-c',
        $Configuration,
        '-f',
        'net10.0',
        '-v',
        'minimal'
    )
}

function Invoke-ModernVerification() {
    $verifyArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $modernVerificationScriptPath,
        '-Configuration',
        $Configuration,
        '-Platform',
        $Platform
    )

    $verifyArguments += @('-PostLaunchDelaySeconds', $PostLaunchDelaySeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture))

    if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
        $verifyArguments += @('-MsBuildPath', $MsBuildPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($ScreenshotPath)) {
        $verifyArguments += @('-ScreenshotPath', $ScreenshotPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $verifyArguments += @('-OutputPath', $OutputPath)
    }

    Invoke-CheckedProcess 'powershell' $verifyArguments
}

function Invoke-ModernPlaybackQualityCheck() {
    $playbackArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $modernPlaybackQualityScriptPath,
        '-Configuration',
        $Configuration,
        '-Platform',
        $Platform
    )

    if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
        $playbackArguments += @('-MsBuildPath', $MsBuildPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $playbackArguments += @('-OutputPath', $OutputPath)
    }

    if ($RequirePlaybackQualityPass) {
        $playbackArguments += @('-RequireQualityPass')
    }

    Invoke-CheckedProcess 'powershell' $playbackArguments
}

Assert-DotNetSdkSupportsModernNet

if ($Target -eq 'Build') {
    Invoke-SolutionBuild (Resolve-ModernMsBuildPath $MsBuildPath) $modernSolutionPath
    return
}

if ($Target -eq 'Check') {
    Invoke-CoreTests
    Invoke-ModernVerification
    return
}

if ($Target -eq 'PlaybackCheck') {
    Invoke-ModernPlaybackQualityCheck
    return
}

if ($Target -eq 'CutoverCheck') {
    Invoke-CutoverCheck
    return
}

if ($Target -eq 'Publish') {
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $modernBuildScriptPath,
        '-Configuration',
        $Configuration,
        '-Platform',
        $Platform,
        '-Target',
        'Publish'
    )

    if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
        $arguments += @('-MsBuildPath', $MsBuildPath)
    }

    Invoke-CheckedProcess 'powershell' $arguments
    return
}

Invoke-ModernVerification
