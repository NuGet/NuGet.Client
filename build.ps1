<#
.SYNOPSIS
Builds NuGet client solutions and runs unit-tests.

.PARAMETER Configuration
Build configuration (debug by default)

.PARAMETER ReleaseLabel
Release label to use for package and assemblies versioning (zlocal by default)

.PARAMETER BuildNumber
Build number to use for package and assemblies versioning (auto-generated if not provided)

.PARAMETER SkipRestore
Builds without restoring first

.PARAMETER CleanCache
Cleans NuGet packages cache before build

.PARAMETER MSPFXPath
Path to a code signing certificate for delay-sigining (optional)

.PARAMETER NuGetPFXPath
Path to a code signing certificate for delay-sigining (optional)

.PARAMETER SkipXProj
Skips building the NuGet.Core XProj projects

.PARAMETER SkipVS14
Skips building binaries targeting Visual Studio "14" (released as Visual Studio 2015)

.PARAMETER SkipVS15
Skips building binaries targeting Visual Studio "15"

.PARAMETER SkipILMerge
Skips creating an ILMerged nuget.exe

.PARAMETER Fast
Runs incremental build.

.PARAMETER CI
Indicates the build script is invoked from CI

.EXAMPLE
To run full clean build, e.g after switching branches:
.\build.ps1

To troubleshoot build issues:
.\build.ps1 -v -ea Stop
#>
[CmdletBinding()]
param (
    [ValidateSet("debug", "release")]
    [Alias('c')]
    [string]$Configuration,
    [ValidateSet("release","rtm", "rc", "rc1", "beta", "beta1", "beta2", "final", "xprivate", "zlocal")]
    [Alias('l')]
    [string]$ReleaseLabel = 'zlocal',
    [Alias('n')]
    [int]$BuildNumber,
    [Alias('sr')]
    [switch]$SkipRestore,
    [Alias('mspfx')]
    [string]$MSPFXPath,
    [Alias('nugetpfx')]
    [string]$NuGetPFXPath,
    [Alias('sx')]
    [switch]$SkipXProj,
    [Alias('s14')]
    [switch]$SkipVS14,
    [Alias('s15')]
    [switch]$SkipVS15,
    [Alias('si')]
    [switch]$SkipILMerge,
    [Alias('f')]
    [switch]$Fast,
    [switch]$CI
)

. "$PSScriptRoot\build\common.ps1"

if (-not $Configuration) {
    $Configuration = switch ($CI.IsPresent) {
        $True   { 'Release' } # CI build is Release by default
        $False  { 'Debug' } # Local builds are Debug by default
    }
}

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

Test-BuildEnvironment -CI:$CI

# Adjust version skipping if only one version installed - if VS15 is not installed, no need to specify SkipVS15
if (-not $SkipVS14 -and -not $VS14Installed) {
    Warning-Log "VS14 build is requested but it appears not to be installed."
    $SkipVS14 = $True
}

if (-not $SkipVS15 -and -not $VS15Installed) {
    Warning-Log "VS15 build is requested but it appears not to be installed."
    $SkipVS15 = $True
}

$BuildErrors = @()

Invoke-BuildStep 'Cleaning artifacts' {
        Clear-Artifacts
        Clear-Nupkgs
    } `
    -skip:($Fast -or $SkipXProj) `
    -ev +BuildErrors

# Restoring tools required for build
Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Enabling delay-signing' {
        param($MSPFXPath, $NuGetPFXPath)
        Enable-DelaySigning $MSPFXPath $NuGetPFXPath
    } `
    -args $MSPFXPath, $NuGetPFXPath `
    -skip:((-not $MSPFXPath) -and (-not $NuGetPFXPath)) `
    -ev +BuildErrors

Invoke-BuildStep 'Building NuGet.Core projects' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore)
        Build-CoreProjects $Configuration $ReleaseLabel $BuildNumber -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore `
    -skip:$SkipXProj `
    -ev +BuildErrors

## Building the VS15 Tooling solution
Invoke-BuildStep 'Building NuGet.Clients projects - VS15 Toolset' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -ToolsetVersion 15 -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore `
    -skip:$SkipVS15 `
    -ev +BuildErrors

## Building the VS14 Tooling solution
Invoke-BuildStep 'Building NuGet.Clients projects - VS14 Toolset' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -ToolsetVersion 14 -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore `
    -skip:$SkipVS14 `
    -ev +BuildErrors

## ILMerge the VS14 exe only
Invoke-BuildStep 'Merging NuGet.exe' {
        param($Configuration, $MSPFXPath)
        Invoke-ILMerge $Configuration 14 $MSPFXPath
    } `
    -args $Configuration, $MSPFXPath `
    -skip:($SkipILMerge -or $SkipVS14) `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}

Write-Host ("`r`n" * 3)
