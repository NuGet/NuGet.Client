. "$PSScriptRoot\Utils.ps1"

function EnableWindowsDeveloperMode()
{
    $windowsDeveloperModeKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
    $success = SetRegistryKey $windowsDeveloperModeKey "AllowDevelopmentWithoutDevLicense" 1 -NeedAdmin
    if (!($success))
    {
        exit 1
    }

    Write-Host -ForegroundColor Cyan 'Windows Developer mode has been enabled.'
}

function DisableTextTemplateSecurityWarning([string]$VSVersion)
{
    $textTemplatingSecurityWarningRegistryKey = Join-Path "HKCU:\SOFTWARE\Microsoft\VisualStudio" $VSVersion
    $textTemplatingSecurityWarningRegistryKey = Join-Path $textTemplatingSecurityWarningRegistryKey `
    "ApplicationPrivateSettings\Microsoft\VisualStudio\TextTemplating\VSHost\OrchestratorOptionsAutomation"

    $success = SetRegistryKey $textTemplatingSecurityWarningRegistryKey "ShowWarningDialog" 1*System.Boolean*False
    if (!($success))
    {
        exit 1
    }

    $message = 'Disabled security message for Text Templates. To re-enable,' `
    + 'Go to Visual Studio -> Tools -> Options -> Text Templating -> Show Security Message. Change it to True.'

    Write-Host -ForegroundColor Cyan $Message
}

trap
{
    Write-Host $_.Exception -ForegroundColor Red
    exit 1
}


$NuGetRoot = Split-Path $PSScriptRoot -Parent

$NuGetTestsPSM1 = Join-Path $NuGetRoot "test\EndToEnd\NuGet.Tests.psm1"

Write-Host 'If successful, this script needs to be run only once on your machine!'

Write-Host 'Setting the environment variable needed to load NuGet.Tests powershell module ' `
'for running functional tests...'

[Environment]::SetEnvironmentVariable("NuGetFunctionalTestPath", $NuGetTestsPSM1, "User")
Write-Host -ForegroundColor Cyan 'You can now call Run-Test from any instance of Visual Studio ' `
'as soon as you open Package Manager Console!'

Write-Host -ForegroundColor Cyan 'Before running all the functional tests, ' `
'please ensure that you have VS Enterprise with F#, Windows Phone tooling and Silverlight installed'

Write-Host
Write-Host 'Trying to set some registry keys to avoid dialog boxes popping during the functional test run...'

$VSVersion = '14.0'
DisableTextTemplateSecurityWarning $VSVersion

$net35x86 = "C:\windows\Microsoft.NET\Framework\v3.5\msbuild.exe"
$net35x64 = "C:\windows\Microsoft.NET\Framework64\v3.5\msbuild.exe"

if (!(Test-Path $net35x86) -or !(Test-Path $net35x64))
{
    Write-Host -ForegroundColor Yellow 'WARNING: .NET 3.5 is not installed on the machine. Please install'
    exit 1
}

EnableWindowsDeveloperMode

Write-Host 'THE END!'