function Get-PlaybackQualitySourceFingerprint {
    param([AllowEmptyString()][string]$Locator)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($(if ($null -eq $Locator) { '' } else { $Locator }))
        $hash = $sha256.ComputeHash($bytes)
        return 'sha256:' + ([System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant())
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-PlaybackQualityReportPath {
    param(
        [Parameter(Mandatory = $true)][string]$ReportsDir,
        [Parameter(Mandatory = $true)][string]$CaseId
    )

    return Join-Path $ReportsDir (
        $CaseId.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + '.json')
}

function Invoke-NativeHeadlessHarnessCase {
    param(
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$StreamUrl,
        [Parameter(Mandatory = $true)][string]$SourceLocatorHash,
        [Parameter(Mandatory = $true)][string]$ReportsDir,
        [Parameter(Mandatory = $true)][string]$NativeHelperExe,
        [Parameter(Mandatory = $true)][string]$HeadlessProjectPath,
        [string]$HarnessScriptPath = '',
        [int]$DurationSeconds = 10,
        [long]$StartPositionTicks = 0,
        [int]$PauseSeconds = 0,
        [ValidateSet('playback', 'timeline', 'interactions', 'pause-resume')]
        [string]$Scenario = 'playback',
        [int]$TimeoutSeconds = 60,
        [bool]$ForceSdrOutput = $false
    )

    $harnessArguments = @(
        '--case-id', $CaseId,
        '--stream-url', $StreamUrl,
        '--source-locator-hash', $SourceLocatorHash,
        '--duration-seconds', ([string]$DurationSeconds),
        '--start-position-ticks', ([string]$StartPositionTicks),
        '--scenario', $Scenario,
        '--timeout-seconds', ([string]$TimeoutSeconds),
        '--reports-dir', $ReportsDir,
        '--native-helper-exe', $NativeHelperExe
    )
    if ($ForceSdrOutput) {
        $harnessArguments += '--force-sdr-output'
    }
    if ($PauseSeconds -gt 0) {
        $harnessArguments += @('--pause-seconds', ([string]$PauseSeconds))
    }

    if (-not [string]::IsNullOrWhiteSpace($HarnessScriptPath)) {
        $commandOutput = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $HarnessScriptPath @harnessArguments)
        $exitCode = $LASTEXITCODE
        $commandOutput | ForEach-Object { Write-Host $_ }
        return [int]$exitCode
    }

    $commandOutput = @(& dotnet run --project $HeadlessProjectPath --no-restore -- @harnessArguments)
    $exitCode = $LASTEXITCODE
    $commandOutput | ForEach-Object { Write-Host $_ }
    return [int]$exitCode
}

function Resolve-PlaybackQualityEmbySource {
    param(
        [Parameter(Mandatory = $true)][string]$ItemId,
        [string]$MediaSourceId = '',
        [Parameter(Mandatory = $true)][string]$ResolverProjectPath,
        [string]$ResolverScriptPath = ''
    )

    $resolverArguments = @(
        '--item-id', $ItemId,
        '--media-source-id', $MediaSourceId
    )
    if (-not [string]::IsNullOrWhiteSpace($ResolverScriptPath)) {
        $output = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $ResolverScriptPath @resolverArguments)
    }
    else {
        $output = @(& dotnet run --project $ResolverProjectPath --no-restore -- resolve-emby-source @resolverArguments)
    }
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        return [pscustomobject]@{ Succeeded = $false; StreamUrl = ''; ErrorCode = 'resolver-failed' }
    }

    $prefix = 'resolved-source-base64:'
    $encoded = @($output | Where-Object { ([string]$_).StartsWith($prefix, [System.StringComparison]::Ordinal) }) |
        Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace([string]$encoded)) {
        return [pscustomobject]@{ Succeeded = $false; StreamUrl = ''; ErrorCode = 'resolver-output-missing' }
    }

    try {
        $streamUrl = [System.Text.Encoding]::UTF8.GetString(
            [Convert]::FromBase64String(([string]$encoded).Substring($prefix.Length)))
    }
    catch {
        return [pscustomobject]@{ Succeeded = $false; StreamUrl = ''; ErrorCode = 'resolver-output-invalid' }
    }

    $parsedUri = $null
    if (-not [Uri]::TryCreate($streamUrl, [UriKind]::Absolute, [ref]$parsedUri) -or
        -not ($streamUrl.StartsWith('http://', [StringComparison]::OrdinalIgnoreCase) -or
            $streamUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase))) {
        return [pscustomobject]@{ Succeeded = $false; StreamUrl = ''; ErrorCode = 'resolver-url-invalid' }
    }

    return [pscustomobject]@{ Succeeded = $true; StreamUrl = $streamUrl; ErrorCode = '' }
}

function Write-PlaybackQualitySourceResolutionError {
    param(
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$SourceLocator,
        [Parameter(Mandatory = $true)][string]$ReportsDir,
        [Parameter(Mandatory = $true)][string]$ErrorCode,
        [Parameter(Mandatory = $true)][string]$ResolverProjectPath
    )

    $output = @(& dotnet run --project $ResolverProjectPath --no-restore -- `
        write-source-resolution-error `
        --case-id $CaseId `
        --source-locator $SourceLocator `
        --reports-dir $ReportsDir `
        --error-code $ErrorCode)
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    return [int]$exitCode
}

Export-ModuleMember -Function `
    Get-PlaybackQualitySourceFingerprint, `
    Get-PlaybackQualityReportPath, `
    Invoke-NativeHeadlessHarnessCase, `
    Resolve-PlaybackQualityEmbySource, `
    Write-PlaybackQualitySourceResolutionError
