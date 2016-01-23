function CleanTempFolder()
{
    if (Test-Path $env:temp)
    {
        Write-Host 'Deleting temp folder'
        rmdir $env:temp -Recurse -ErrorAction SilentlyContinue
        Write-Host 'Done.'
    }
}

function IsAdminPrompt()
{
    If (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(`
        [Security.Principal.WindowsBuiltInRole] "Administrator"))
    {
        return $false
    }

    return $true
}

function GetRegistryKey
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$RegKey,
    [Parameter(Mandatory=$true)]
    [string]$RegName)

    if (!(Test-Path -Path $RegKey -PathType Container))
    {
        return $null
    }

    $props = Get-ItemProperty -Path $RegKey
    if (!($props) -or ($props.Length -eq 0))
    {
        return $null
    }

    $name = Get-Member -InputObject $props -Name $RegName
    if (!($name))
    {
        return $null
    }

    $value = Get-ItemPropertyValue -Path $RegKey -Name $RegName
    return $value
}

function SetRegistryKey
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$RegKey,
    [Parameter(Mandatory=$true)]
    [string]$RegName,
    [Parameter(Mandatory=$true)]
    $ExpectedValue,
    $FriendlyKeyName = "the provided registry key",
    [switch]$NeedAdmin)

    $currentValue = GetRegistryKey $RegKey $RegName

    Write-Host "Current value is $currentValue"
    if ($currentValue -eq $ExpectedValue)
    {
        Write-Host -ForegroundColor Cyan "Registry settings for $FriendlyKeyName is already as desired."
        return $true
    }
    else
    {
        if ($NeedAdmin -and !(IsAdminPrompt))
        {
            return $false
        }

        New-Item -Path $RegKey -Name $RegName -Value $ExpectedValue -Force
    }

    return $true
}


function DisableCrashDialog()
{
    # Set the registry key to prevent the 'Not Responding' window from showing up on a crash
    $result1 = SetRegistryKey "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting" "ForceQueue" 1 -NeedAdmin
    $result2 = SetRegistryKey "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\Consent" "DefaultConsent" 1 -NeedAdmin

    If (($result1 -eq $false) -or ($result2 -eq $false))
    {
        $warningMessage = 'WARNING: Please re-run this script as an Administrator! Setting registry keys need admin privileges.'

        Write-Host -ForegroundColor Yellow $warningMessage
        exit 1
    }

    Write-Host -ForegroundColor Cyan 'Windows crash dialog has been disabled'
}