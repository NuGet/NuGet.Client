param (
    [Parameter(Mandatory=$true)]
    [string]$FuncTestRoot)

function CopyNuGetCITools
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetTestPath
    )

    Write-Host 'Trying to copy necessary tools to ' $NuGetTestPath

    Copy-Item $NuGetTestPath\tools\*.exe $NuGetTestPath
    Copy-Item $NuGetTestPath\tools\*.exe.config $NuGetTestPath
    Copy-Item $NuGetTestPath\tools\*.dll $NuGetTestPath

    Write-Host 'Copied the necessary tools to test path ' $NuGetTestPath
}

. "$PSScriptRoot\Utils.ps1"
. "$PSScriptRoot\NuGetFunctionalTestUtils.ps1"

$success = IsAdminPrompt

if ($success -eq $false)
{
    $errorMessage = 'ERROR: Please re-run this script as an Administrator! ' +
    'Updating registry require admin privileges.'

    Write-Error $errorMessage
    exit 1
}


$NuGetTestPath = Join-Path $FuncTestRoot "EndToEnd"
CopyNuGetCITools $NuGetTestPath

# Already checked if the prompt is an admin prompt
DisableCrashDialog