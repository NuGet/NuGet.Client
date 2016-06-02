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
    [switch]$CleanCache
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

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow

$FuncScriptsRoot = Split-Path -Path $PSScriptRoot -Parent
$NuGetClientRoot = Split-Path -Path $FuncScriptsRoot -Parent
$FuncTestRoot = Join-Path $NuGetClientRoot "test\NuGet.Core.FuncTests"
$SrcRoot = Join-Path $NuGetClientRoot "src\NuGet.Core"

Write-Host "Dependent Build Details are as follows:"
Write-Host "Branch: $DepBuildBranch"
Write-Host "Commit ID: $DepCommitID"
Write-Host "Build Number: $DepBuildNumber"
Write-Host ""

$BuildErrors = @()

Invoke-BuildStep 'Installing runtime' { Install-DNX CoreCLR; Install-DNX CLR -Default } `
    -ev +BuildErrors

Invoke-BuildStep 'Updating sub-modules' { Update-SubModules } `
    -skip:($SkipSubModules -or $Fast) `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring func test projects' { Restore-XProjects -Fast -XProjectsLocation $FuncTestRoot } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring src projects' { Restore-XProjects -Fast -XProjectsLocation $SrcRoot } `
    -ev +BuildErrors

# Run tests
Invoke-BuildStep 'Running tests' {
    param($FuncTestRoot)
    $xtests = Find-XProjects $FuncTestRoot
    $xtests | Test-XProject
} -args $FuncTestRoot -ev +BuildErrors

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