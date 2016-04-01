param (
    [string]$DepBuildBranch="",
    [string]$DepCommitID="",
    [string]$DepBuildNumber="",
    [switch]$CleanCache
)

# For TeamCity - Incase any issue comes in this script fail the build. - Be default TeamCity returns exit code of 0 for all powershell even if it fails
trap
{
    Write-Host "Build failed: $_" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\..\..\build\common.ps1"

$FuncScriptsRoot = Split-Path -Path $PSScriptRoot -Parent
$NuGetClientRoot = Split-Path -Path $FuncScriptsRoot -Parent
$FuncTestRoot = Join-Path $NuGetClientRoot "test\\NuGet.Core.FuncTests"
$SrcRoot = Join-Path $NuGetClientRoot "src\\NuGet.Core"

pushd $NuGetClientRoot

Write-Host "Dependent Build Details are as follows:"
Write-Host "Branch: $DepBuildBranch"
Write-Host "Commit ID: $DepCommitID"
Write-Host "Build Number: $DepBuildNumber"
Write-Host ""

$BuildErrors = @()

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

Invoke-BuildStep 'Installing dotnet CLI' { Install-DotnetCLI } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring projects' { Restore-XProjects } `
    -ev +BuildErrors

# Run tests
$xtests = Find-XProjects $FuncTestRoot
$xtests | Test-XProject -ev +BuildErrors

popd

if ($BuildErrors) {
    Trace-Log "Build's completed with following errors:"
    $BuildErrors | Out-Default
}

Trace-Log ('=' * 60)

if ($BuildErrors) {
    Throw $BuildErrors.Count
}

# Return success
exit 0