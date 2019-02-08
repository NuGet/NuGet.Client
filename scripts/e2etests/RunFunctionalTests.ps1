param (
    [Parameter(Mandatory=$true)]
    [string]$PMCCommand,
    [Parameter(Mandatory=$true)]
    [int]$PMCLaunchWaitTimeInSecs,
    [Parameter(Mandatory=$true)]
    [int]$EachTestTimoutInSecs,
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [string]$FuncTestRoot,
    [Parameter(Mandatory=$true)]
    [string]$RunCounter,
    [Parameter(Mandatory=$true)]
	[ValidateSet("16.0")]
    [string]$VSVersion)

trap
{
    Write-Host "RunFunctionalTests.ps1 threw an exception: " $_.Exception -ForegroundColor Red
    KillRunningInstancesOfVS
    exit 1
}

. "$PSScriptRoot\Utils.ps1"
. "$PSScriptRoot\VSUtils.ps1"
. "$PSScriptRoot\NuGetFunctionalTestUtils.ps1"

$NuGetTestPath = Join-Path $FuncTestRoot "EndToEnd"

Write-Host 'NuGetTestPath is ' $NuGetTestPath

Write-Host 'Before starting the functional tests, force delete all the Results.html under the tests folder'
(Get-ChildItem $NuGetTestPath -Recurse Results.html) | Remove-Item -Force

CleanTempFolder

$result = LaunchVSAndWaitForDTE -VSVersion $VSVersion -DTEReadyPollFrequencyInSecs 6 -NumberOfPolls 50
if ($result -eq $true) {
    Write-Host 'Do the kill VS, Launch VS and wait for DTE one more time'
    $result = LaunchVSAndWaitForDTE -VSVersion $VSVersion -DTEReadyPollFrequencyInSecs 6 -NumberOfPolls 50 -ActivityLogFullPath $env:ActivityLogFullPath
    if ($result -eq $false) {
        Write-Error "Could not obtain DTE after waiting $NumberOfPolls * $DTEReadyPollFrequencyInSecs = " $NumberOfPolls * $DTEReadyPollFrequencyInSecs " secs"
        exit 1
    }
}

$dte2 = GetDTE2 $VSVersion

if (!$dte2)
{
    Write-Error 'DTE could not be obtained'
    KillRunningInstancesOfVS
    exit 1
}

Write-Host "Launching the Package Manager Console inside VS and waiting for $PMCLaunchWaitTimeInSecs seconds"
ExecuteCommand $dte2 "View.PackageManagerConsole" $null "Opening NuGet Package Manager Console" $PMCLaunchWaitTimeInSecs

Write-Host "Set the execution policy on the process to be Bypass and wait for a second. This operation is very fast"
ExecuteCommand $dte2 "View.PackageManagerConsole" "Set-ExecutionPolicy Bypass -Scope Process -Force" "Running command: 'Set-ExecutionPolicy Bypass -Scope Process -Force' ..." 1

Write-Host "Remove any NuGet.Tests module that may have been loaded already and wait for a second. This operation is very fast"
ExecuteCommand $dte2 "View.PackageManagerConsole" "Get-Module NuGet.Tests | Remove-Module" "Running command: 'Get-Module NuGet.Tests | Remove-Module' ..." 1

$NuGetTestsModulePath = Join-Path $PSScriptRoot "NuGet.Tests.psd1"
if (-not (Test-Path $NuGetTestsModulePath))
{
    $NuGetTestsModulePath = Join-Path $NuGetTestPath "NuGet.Tests.psd1"
}

Write-Host "Import NuGet.Tests module from $NuGetTestPath and wait for 5 seconds."
ExecuteCommand $dte2 "View.PackageManagerConsole" "Import-Module $NuGetTestsModulePath" "Running command: 'Import-Module $NuGetTestsModulePath' ..." 5

Write-Host "Executing the provided Package manager console command: ""$PMCCommand"""
ExecuteCommand $dte2 "View.PackageManagerConsole" $PMCCommand "Running command: $PMCCommand ..."

Write-Host "Starting functional tests with command '$PMCCommand'"
RealTimeLogResults $NuGetTestPath $EachTestTimoutInSecs

KillRunningInstancesOfVS



Write-Host -ForegroundColor Cyan "THE END!"