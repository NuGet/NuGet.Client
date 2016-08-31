<#
.SYNOPSIS
Build and run functional tests

.PARAMETER DepBuildBranch
Build branch (unused)

.PARAMETER DepCommitID
Commit ID (unused)

.PARAMETER DepBuildNumber
Build number (unused)

.PARAMETER CleanCache
Switch to clean local package cache

.EXAMPLE
.\scripts\funcTests\runFuncTests.ps1 -Verbose
#>
[CmdletBinding()]
param (
    [string]$DepBuildBranch="",
    [string]$DepCommitID="",
    [string]$DepBuildNumber="",
    [switch]$CleanCache,
    [switch]$SkipVS14,
    [switch]$SkipVS15
)

# For TeamCity - Incase any issue comes in this script fail the build. - Be default TeamCity returns exit code of 0 for all powershell even if it fails
trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\..\..\build\common.ps1"

# Adjust version skipping if only one version installed - if VS15 is not installed, no need to specify SkipVS15
$VS14Installed = Test-MSBuildVersionPresent -MSBuildVersion "14"
$SkipVS14 = $SkipVS14 -or -not $VS14Installed

$VS15Installed = Test-MSBuildVersionPresent -MSBuildVersion "15"
$SkipVS15 = $SkipVS15 -or -not $VS15Installed

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow

Write-Host "Dependent Build Details are as follows:"
Write-Host "Branch: $DepBuildBranch"
Write-Host "Commit ID: $DepCommitID"
Write-Host "Build Number: $DepBuildNumber"
Write-Host ""

$BuildErrors = @()

Invoke-BuildStep 'Updating sub-modules' { Update-SubModules } `
    -skip:($SkipSubModules -or $Fast) `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Installing dotnet CLI' { Install-DotnetCLI } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring projects' { Restore-XProjects } `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Core functional tests' { Test-FuncCoreProjects } `
    -ev +BuildErrors

Invoke-BuildStep 'Building NuGet.Clients projects - VS15 dependencies' {
        Build-ClientsProjects -MSBuildVersion "15"
    } `
    -skip:$SkipVS15 `
    -ev +BuildErrors

Invoke-BuildStep 'Building NuGet.Clients projects - VS14 dependencies' {
        Build-ClientsProjects -MSBuildVersion "14"
    } `
    -skip:$SkipVS14 `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients functional tests - VS15 dependencies' {
        # We don't run command line tests on VS15 as we don't build a nuget.exe for this version
        Test-FuncClientsProjects -MSBuildVersion "15" -SkipProjects 'NuGet.CommandLine.FuncTest'
    } `
    -skip:$SkipVS15 `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients functional tests - VS14 dependencies' {
        Test-FuncClientsProjects -MSBuildVersion "14"
    } `
    -skip:$SkipVS14 `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build completed at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)

# Return success
exit 0