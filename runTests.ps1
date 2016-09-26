<#
.SYNOPSIS
Build and run unit-tests and functional tests.

.PARAMETER Configuration
Build configuration (debug by default)

.PARAMETER SkipXProj
Skips running NuGet.Core.Tests and NuGet.Core.FuncTests

.PARAMETER SkipVS14
Skips running NuGet.Clients.Tests and NuGet.Clients.FuncTests with VS14 toolset

.PARAMETER SkipVS15
Skips running NuGet.Clients.Tests and NuGet.Clients.FuncTests with VS15 toolset

.PARAMETER SkipUnitTests
Skips running NuGet.Core.Tests and NuGet.Clients.Tests

.PARAMETER SkipFuncTests
Skips running NuGet.Core.FuncTests and NuGet.Clients.FuncTests

.PARAMETER CI
Indicates the build script is invoked from CI

.EXAMPLE
Running full test suite:
.\runTests.ps1 -Verbose

Running functional tests only:
.\runTests.ps1 -sut

Running core unit tests only:
.\runTests.ps1 -sft -s14 -s15
#>
[CmdletBinding()]
param (
    [ValidateSet("debug", "release")]
    [Alias('c')]
    [string]$Configuration,
    [Alias('sx')]
    [switch]$SkipXProj,
    [Alias('s14')]
    [switch]$SkipVS14,
    [Alias('s15')]
    [switch]$SkipVS15,
    [Alias('sut')]
    [switch]$SkipUnitTests,
    [Alias('sft')]
    [switch]$SkipFuncTests,
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

Trace-Log "Test suite run #$BuildNumber started at $startTime"

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

# Restoring tools required for build
Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Core unit-tests' {
        param($Configuration)
        Test-CoreProjects $Configuration
    } `
    -args $Configuration `
    -skip:($SkipXProj -or $SkipUnitTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Core functional tests' {
        param($Configuration)
        Test-FuncCoreProjects $Configuration
    } `
    -args $Configuration `
    -skip:($SkipXProj -or $SkipFuncTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Building NuGet.Clients projects - VS14 Toolset' {
        param($Configuration, $ReleaseLabel, $BuildNumber)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -ToolsetVersion 14
    } `
    -args $Configuration, $DefaultReleaseLabel, $BuildNumber `
    -skip:$SkipVS14 `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients unit-tests - VS14 Toolset' {
        param($Configuration)
        Test-ClientsProjects $Configuration -ToolsetVersion 14
    } `
    -args $Configuration `
    -skip:($SkipVS14 -or $SkipUnitTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients functional tests - VS14 Toolset' {
        param($Configuration)
        Test-FuncClientsProjects $Configuration -ToolsetVersion 14
    } `
    -args $Configuration `
    -skip:($SkipVS14 -or $SkipFuncTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Building NuGet.Clients projects - VS15 Toolset' {
        param($Configuration, $ReleaseLabel, $BuildNumber)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -ToolsetVersion 15
    } `
    -args $Configuration, $DefaultReleaseLabel, $BuildNumber `
    -skip:$SkipVS15 `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients tests - VS15 Toolset' {
        param($Configuration)
        # We don't run command line tests on VS15 as we don't build a nuget.exe for this version
        Test-ClientsProjects $Configuration -ToolsetVersion 15 -SkipProjects 'NuGet.CommandLine.Test'
    } `
    -args $Configuration `
    -skip:($SkipVS15 -or $SkipUnitTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients functional tests - VS15 Toolset' {
        param($Configuration)
        # We don't run command line tests on VS15 as we don't build a nuget.exe for this version
        Test-FuncClientsProjects $Configuration -ToolsetVersion 15 -SkipProjects 'NuGet.CommandLine.FuncTest'
    } `
    -args $Configuration `
    -skip:($SkipVS15 -or $SkipFuncTests) `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Test suite run has completed at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}

Write-Host ("`r`n" * 3)
