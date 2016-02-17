param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [string]$FuncTestRoot,
    [Parameter(Mandatory=$true)]
    [string]$NuGetVSIXID,
    [Parameter(Mandatory=$true)]
    [int]$VSIXInstallerWaitTimeInSecs,
    [Parameter(Mandatory=$true)]
	[ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
    [string]$VSVersion)

. "$PSScriptRoot\Utils.ps1"
. "$PSScriptRoot\VSUtils.ps1"

$success = IsAdminPrompt

if ($success -eq $false)
{
    $errorMessage = 'ERROR: Please re-run this script as an Administrator! ' +
    'Actions such as installing VSIX and uninstalling VSIX require admin privileges.'

    Write-Error $errorMessage
    exit 1
}

KillRunningInstancesOfVS

start-sleep -Seconds $VSIXInstallerWaitTimeInSecs

$VSIXSrcPath = Join-Path $NuGetDropPath 'NuGet.Tools.vsix'
$VSIXPath = Join-Path $FuncTestRoot 'NuGet.Tools.vsix'

Copy-Item $VSIXSrcPath $VSIXPath

$success = UninstallVSIX $NuGetVSIXID $VSVersion $VSIXInstallerWaitTimeInSecs
if ($success -eq $false)
{
    exit 1
}

$success = InstallVSIX $VSIXPath $VSVersion $VSIXInstallerWaitTimeInSecs
if ($success -eq $false)
{
    exit 1
}