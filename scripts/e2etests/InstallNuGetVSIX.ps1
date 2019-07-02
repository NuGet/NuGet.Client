param (
    [Parameter(Mandatory = $true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory = $true)]
    [string]$FuncTestRoot,
    [Parameter(Mandatory = $true)]
    [string]$NuGetVSIXID,
    [Parameter(Mandatory = $true)]
    [int]$ProcessExitTimeoutInSeconds,
    [Parameter(Mandatory = $true)]
    [ValidateSet("16.0")]
    [string]$VSVersion)

. "$PSScriptRoot\VSUtils.ps1"

$success = IsAdminPrompt

if ($success -eq $false) {
    $errorMessage = 'ERROR: Please re-run this script as an administrator! ' +
    'Actions such as installing VSIX and uninstalling VSIX require admin privileges.'

    Write-Error $errorMessage
    exit 1
}

$VSIXSrcPath = Join-Path $NuGetDropPath 'NuGet.Tools.vsix'
$VSIXPath = Join-Path $FuncTestRoot 'NuGet.Tools.vsix'

Copy-Item $VSIXSrcPath $VSIXPath

# Because we are upgrading an installed system component VSIX, we need to downgrade first.
$numberOfTries = 0
$success = $false
do {
    KillRunningInstancesOfVS
    $numberOfTries++
    Write-Host "Attempt # $numberOfTries to downgrade VSIX..."
    $success = DowngradeVSIX $NuGetVSIXID $VSVersion $ProcessExitTimeoutInSeconds
}
until (($success -eq $true) -or ($numberOfTries -gt 3))

# Clearing MEF cache helps load the right dlls for VSIX
ClearMEFCache


$numberOfTries = 0
$success = $false
do {
    KillRunningInstancesOfVS
    $numberOfTries++
    Write-Host "Attempt # $numberOfTries to install VSIX..."
    $success = InstallVSIX $VSIXPath $VSVersion $ProcessExitTimeoutInSeconds
}
until (($success -eq $true) -or ($numberOfTries -gt 3))

if ($success -eq $false) {
    exit 1
}

ClearMEFCache
Update-Configuration