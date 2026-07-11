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
        [ValidateSet('playback', 'timeline', 'audio-switch', 'subtitle-switch', 'pause-resume')]
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
    $prefix = 'resolved-source-base64:'
    $lastErrorCode = 'resolver-failed'
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            if (-not [string]::IsNullOrWhiteSpace($ResolverScriptPath)) {
                $output = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $ResolverScriptPath @resolverArguments 2>&1)
            }
            else {
                $output = @(& dotnet run --project $ResolverProjectPath --no-restore -- resolve-emby-source @resolverArguments 2>&1)
            }
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        if ($exitCode -eq 0) {
            $encoded = @($output | Where-Object { ([string]$_).StartsWith($prefix, [System.StringComparison]::Ordinal) }) |
                Select-Object -Last 1
            if (-not [string]::IsNullOrWhiteSpace([string]$encoded)) {
                try {
                    $streamUrl = [System.Text.Encoding]::UTF8.GetString(
                        [Convert]::FromBase64String(([string]$encoded).Substring($prefix.Length)))
                    $parsedUri = $null
                    if ([Uri]::TryCreate($streamUrl, [UriKind]::Absolute, [ref]$parsedUri) -and
                        ($streamUrl.StartsWith('http://', [StringComparison]::OrdinalIgnoreCase) -or
                            $streamUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase))) {
                        return [pscustomobject]@{
                            Succeeded = $true
                            StreamUrl = $streamUrl
                            ErrorCode = ''
                            AttemptCount = $attempt
                        }
                    }
                    $lastErrorCode = 'resolver-url-invalid'
                }
                catch {
                    $lastErrorCode = 'resolver-output-invalid'
                }
            }
            else {
                $lastErrorCode = 'resolver-output-missing'
            }
        }
        else {
            $resolverError = @($output | ForEach-Object { [string]$_ } |
                Select-String -Pattern 'resolver-error:([A-Za-z0-9._-]+)' -AllMatches |
                ForEach-Object { $_.Matches } |
                ForEach-Object { $_.Groups[1].Value }) | Select-Object -Last 1
            $lastErrorCode = if ([string]::IsNullOrWhiteSpace([string]$resolverError)) {
                'resolver-failed'
            } else {
                [string]$resolverError
            }
        }

        if ($attempt -lt 3) {
            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }

    return [pscustomobject]@{
        Succeeded = $false
        StreamUrl = ''
        ErrorCode = $lastErrorCode
        AttemptCount = 3
    }
}

function Write-PlaybackQualitySourceResolutionError {
    param(
        [Parameter(Mandatory = $true)][string]$CaseId,
        [Parameter(Mandatory = $true)][string]$SourceLocator,
        [Parameter(Mandatory = $true)][string]$ReportsDir,
        [Parameter(Mandatory = $true)][string]$ErrorCode,
        [ValidateSet('playback', 'timeline', 'audio-switch', 'subtitle-switch', 'pause-resume')]
        [string]$Scenario = 'playback',
        [Parameter(Mandatory = $true)][string]$ResolverProjectPath
    )

    $output = @(& dotnet run --project $ResolverProjectPath --no-restore -- `
        write-source-resolution-error `
        --case-id $CaseId `
        --source-locator $SourceLocator `
        --reports-dir $ReportsDir `
        --error-code $ErrorCode `
        --scenario $Scenario)
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
