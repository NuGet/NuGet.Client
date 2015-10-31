param (
    [string]$NuGetRoot=$null
)

if (!$NuGetRoot)
{
    $NuGetRoot = $pwd
}

$NuGetTestsPSM1 = Join-Path $NuGetRoot "test\EndToEnd\NuGet.Tests.psm1"

Write-Host 'Setting the environment variable needed to load NuGet.Tests powershell module for running functional tests...'
[Environment]::SetEnvironmentVariable("NuGetFunctionalTestPath", $NuGetTestsPSM1, "User")
Write-Host 'You can now call Run-Test from any instance of Visual Studio as soon as you open Package Manager Console!'
Write-Host
Write-Host 'Trying to some registry keys to avoid dialog boxes popping during the functional test run...'

If (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(`
    [Security.Principal.WindowsBuiltInRole] "Administrator"))
{
    $warningMessage = 'Please re-run this script as an Administrator! Setting registry keys need admin privileges. If you are not planning to run all the tests, you can ignore this message'

    Write-Host -ForegroundColor Yellow $warningMessage
    Break
}

# Set the registry key to prevent the 'Not Responding' window from showing up on a crash
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting" -Name ForceQueue -Value 1
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\Consent" -Name DefaultConsent -Value 1

# Set the registry key to enable Windows Developer Mode
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Name AllowDevelopmentWithoutDevLicense -Value 1

Write-Host 'THE END!'