function ExtractZip($source, $destination)
{
    Write-Host 'Extracting files from ' $source ' to ' $destination '...'

    $shell = New-Object -ComObject Shell.Application
    $zip = $shell.NameSpace($source)
    $files = $zip.Items()
    # 0x14 means that the existing files will be overwritten silently
    $timeTaken = measure-command { $shell.NameSpace($destination).CopyHere($files, 0x14) }
    Write-Host 'Extraction Completed in ' $timeTaken.TotalSeconds ' seconds.'
}

function CleanPaths($NuGetTestPath)
{
    if (Test-Path $NuGetTestPath)
    {
        Write-Host 'Deleting ' $NuGetTestPath ' test path before running tests...'
        rmdir -Recurse $NuGetTestPath -Force

        if (Test-Path $NuGetTestPath)
        {
            Write-Error 'Could not delete folder ' $NuGetTestPath
            exit 1
        }

        Write-Host 'Done.'
    }
}

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

function DisableCrashDialog()
{
    $success = IsAdminPrompt
    If ($success -eq $false)
    {
        $warningMessage = 'WARNING: Please re-run this script as an Administrator! Setting registry keys need admin privileges.'

        Write-Host -ForegroundColor Yellow $warningMessage
        return $false
    }

    # Set the registry key to prevent the 'Not Responding' window from showing up on a crash
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting" -Name ForceQueue -Value 1
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\Consent" -Name DefaultConsent -Value 1

    Write-host 'Windows error reporting Crash dialog has been disabled.'
    return $true
}