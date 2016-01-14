[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [ValidateSet("Release","rtm", "rc", "beta", "local")]
    [string]$ReleaseLabel = 'local',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [switch]$DelaySign,
    [string]$MSPFXPath,
    [string]$NuGetPFXPath,
    [switch]$SkipXProj,
    [switch]$SkipCSProj,
    [Parameter(ParameterSetName='RegularBuild')]
    [switch]$SkipSubModules,
    [Parameter(ParameterSetName='RegularBuild')]
    [switch]$SkipTests,
    [Parameter(ParameterSetName='RegularBuild')]
    [switch]$SkipILMerge,
    [Parameter(ParameterSetName='FastBuild')]
    [switch]$Fast
)

# For TeamCity - Incase any issue comes in this script fail the build. - Be default TeamCity returns exit code of 0 for all powershell even if it fails
trap
{
    Write-Host "Build failed: $_" -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

$RunTests = (-not $SkipTests) -and (-not $Fast)

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

# Move to the script directory
pushd $NuGetClientRoot

$BuildErrors = @()
Invoke-BuildStep 'Updating sub-modules' { Update-SubModules } `
    -skip:($SkipSubModules -or $Fast) `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Cleaning artifacts' { Clear-Artifacts } `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Cleaning nupkgs' { Clear-Nupkgs } -skip:$SkipXProj `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } -skip:(-not $CleanCache) `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors -ea $ErrorActionPreference

# Restoring tools required for build
Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -skip:$SkipRestore `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Installing runtime' { Install-DNX CoreCLR; Install-DNX CLR -Default } `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Enabling delayed signing' {
        param($MSPFXPath, $NuGetPFXPath) Enable-DelayedSigning $MSPFXPath $NuGetPFXPath
    } `
    -args $MSPFXPath, $NuGetPFXPath `
    -skip:(-not $DelaySign) `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Building NuGet.Core projects' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast)
        Build-CoreProjects $Configuration $ReleaseLabel $BuildNumber -SkipRestore:$SkipRestore -Fast:$Fast
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast `
    -skip:$SkipXProj `
    -ev +BuildErrors -ea $ErrorActionPreference

## Building the Tooling solution
Invoke-BuildStep 'Building NuGet.Clients projects' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -SkipRestore:$SkipRestore -Fast:$Fast
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast `
    -skip:$SkipCSproj `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Running NuGet.Core tests' {
        param($SkipRestore, $Fast)
        Test-CoreProjects -SkipRestore:$SkipRestore -Fast:$Fast
    } `
    -args $SkipRestore, $Fast `
    -skip:($SkipXProj -or (-not $RunTests)) `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Running NuGet.Clients tests' {
        param($Configuration) Test-ClientsProjects $Configuration
    } `
    -args $Configuration `
    -skip:($SkipCSproj -or (-not $RunTests)) `
    -ev +BuildErrors -ea $ErrorActionPreference

Invoke-BuildStep 'Merging NuGet.exe' {
        param($Configuration) Invoke-ILMerge $Configuration
    } `
    -args $Configuration `
    -skip:($SkipILMerge -or $SkipCSProj -or $Fast) `
    -ev +BuildErrors -ea $ErrorActionPreference

popd

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

if ($BuildErrors) {
    Trace-Log "Build's completed with following errors:"
    $BuildErrors | Out-Default
}

Trace-Log ('=' * 60)

if ($BuildErrors) {
    Throw $BuildErrors.Count
}

Write-Host ("`r`n" * 3)