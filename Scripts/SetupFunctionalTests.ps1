param (
    [string]$NuGetRoot=$null
)

function EnableWindowsDeveloperMode()
{
    $windowsDeveloperModeKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
    if (Test-Path $windowsDeveloperModeKey)
    {
        Set-ItemProperty -Path $windowsDeveloperModeKey -Name AllowDevelopmentWithoutDevLicense -Value 1
        return $true
    }
    else {
        return $false
    }
}

function DisableTextTemplateSecurityWarning([string]$VSVersion)
{
    $textTemplatingSecurityWarningRegistryKey = "HKCU:\SOFTWARE\Microsoft\VisualStudio\" + $VSVersion + "\ApplicationPrivateSettings\Microsoft\VisualStudio\TextTemplating\VSHost\OrchestratorOptionsAutomation"

    if (Test-Path $textTemplatingSecurityWarningRegistryKey)
    {
        Set-ItemProperty -Path $textTemplatingSecurityWarningRegistryKey -Name ShowWarningDialog -Value 1*System.Boolean*False
        return $True
    }
    else {
        return $false
    }
}

if (!$NuGetRoot)
{
    $NuGetRoot = $pwd
}

$NuGetTestsPSM1 = Join-Path $NuGetRoot "test\EndToEnd\NuGet.Tests.psm1"

Write-Host 'If successful, this script needs to be run only once on your machine!'

Write-Host 'Setting the environment variable needed to load NuGet.Tests powershell module for running functional tests...'
[Environment]::SetEnvironmentVariable("NuGetFunctionalTestPath", $NuGetTestsPSM1, "User")
Write-Host -ForegroundColor Cyan 'You can now call Run-Test from any instance of Visual Studio as soon as you open Package Manager Console!'
Write-Host -ForegroundColor Cyan 'Before running all the functional tests, please ensure that you have VS Enterprise with F#, Windows Phone tooling and Silverlight installed'
Write-Host
Write-Host 'Trying to set some registry keys to avoid dialog boxes popping during the functional test run...'

If (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(`
    [Security.Principal.WindowsBuiltInRole] "Administrator"))
{
    $warningMessage = 'WARNING: Please re-run this script as an Administrator! Setting registry keys need admin privileges. If you are not planning to run all the tests, you can ignore this message'

    Write-Host -ForegroundColor Yellow $warningMessage
    Break
}

$result = EnableWindowsDeveloperMode

if (!$result)
{
    Write-Host -ForegroundColor Yellow 'WARNING: Could not enable windows developer mode. Registry Key not found'
}
else {
    Write-Host -ForegroundColor Cyan 'Windows Developer mode has been enabled.'
}


$VSVersion = '14.0'
$result = DisableTextTemplateSecurityWarning $VSVersion
if (!$result)
{
    Write-Host -ForegroundColor Yellow 'WARNING: Currently, functional tests can only be run on VS 2015 and could not find settings on registry for that. Skipping...'
}
else {
    Write-Host -ForegroundColor Cyan 'Disabled security message for Text Templates. To re-enable, Go to Visual Studio -> Tools -> Options -> Text Templating -> Show Security Message. Change it to True.'
}

Write-Host 'THE END!'