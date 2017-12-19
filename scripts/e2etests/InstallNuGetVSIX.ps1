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
    [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
    [string]$VSVersion)

. "$PSScriptRoot\VSUtils.ps1"

$success = IsAdminPrompt

if ($success -eq $false) {
    $errorMessage = 'ERROR: Please re-run this script as an Administrator! ' +
    'Actions such as installing VSIX and uninstalling VSIX require admin privileges.'

    Write-Error $errorMessage
    exit 1
}

$VSIXSrcPath = Join-Path $NuGetDropPath 'NuGet.Tools.vsix'
$VSIXPath = Join-Path $FuncTestRoot 'NuGet.Tools.vsix'

Copy-Item $VSIXSrcPath $VSIXPath

# Since dev14 vsix is not uild with vssdk 3.0, we can uninstall and re installing
# For dev 15, we upgrade an installed system component vsix
if ($VSVersion -eq '14.0') {
    KillRunningInstancesOfVS
    $success = UninstallVSIX $NuGetVSIXID $VSVersion $ProcessExitTimeoutInSeconds
    if ($success -eq $false) {
        exit 1
    }
}
else {
    $numberOfTries = 0
    $success = $false
    do {
        KillRunningInstancesOfVS
        $numberOfTries++
        Write-Host "Attempt # $numberOfTries to downgrade VSIX..."
        $success = DowngradeVSIX $NuGetVSIXID $VSVersion $ProcessExitTimeoutInSeconds
    }
    until (($success -eq $true) -or ($numberOfTries -gt 3))    

    # Clearing MEF cache helps load the right dlls for vsix
    ClearDev15MEFCache
}

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
