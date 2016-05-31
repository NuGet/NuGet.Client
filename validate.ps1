<#
.SYNOPSIS
Runs validation scenarios for given nuget.exe

.DESCRIPTION
Installs and imports Pester module from NuGet package.
Invokes Pester runner with nuget.exe validation scenarios.

.PARAMETER TargetNuGetExe
Path to a nuget.exe to run validation scenarios against

.PARAMETER SkipRestore
Skips restore packages

.EXAMPLE
.\validate.ps1 C:\temp\nuget.exe

.EXAMPLE
.\validate.ps1 -t C:\temp\nuget.exe -sr
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory=$True, Position=0)]
    [Alias('t')]
    [string]$TargetNuGetExe,
    [Alias('sr')]
    [switch]$SkipRestore
)

. "$PSScriptRoot\build\common.ps1"

if (-not $SkipRestore) {
    Install-NuGet
    Restore-SolutionPackages
}

if (-not (Get-Module Pester)) {
    Import-Module "$PSScriptRoot\packages\Pester.3.4.0\tools\Pester"
}

if (-not $TargetNuGetExe) {
    $TargetNuGetExe = "$PSScriptRoot\artifacts\NuGet.exe"
}

Invoke-Pester -Script @{
    Path = "$PSScriptRoot\test\ValidationScenarios\NuGetExe.Config*"
    Parameters =@{ NuGetExe = $TargetNuGetExe }
}
