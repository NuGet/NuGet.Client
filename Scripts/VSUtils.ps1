function GetVSFolderPath
{
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
        [string]$VSVersion
    )

    $ProgramFilesPath = ${env:ProgramFiles}
    if (Test-Path ${env:ProgramFiles(x86)})
    {
        $ProgramFilesPath = ${env:ProgramFiles(x86)}
    }

    $VSFolderPath = Join-Path $ProgramFilesPath ("Microsoft Visual Studio " + $VSVersion)

    return $VSFolderPath
}

function GetVSIDEFolderPath
{
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
        [string]$VSVersion
    )

    $VSFolderPath = GetVSFolderPath $VSVersion
    $VSIDEFolderPath = Join-Path $VSFolderPath "Common7\IDE"

    return $VSIDEFolderPath
}

function KillRunningInstancesOfVS
{
    Write-Host 'Kill any running instances of devenv...'
    (Get-Process 'devenv' -ErrorAction SilentlyContinue) | Kill -ErrorAction SilentlyContinue
}

function LaunchVS
{
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
        [string]$VSVersion
    )

    $VSIDEFolderPath = GetVSIDEFolderPath $VSVersion
    $VSPath = Join-Path $VSIDEFolderPath "devenv.exe"
    Write-Host 'Starting ' $VSPath
    start-process $VSPath
}

function GetDTE2
{
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
        [string]$VSVersion
    )

    Try
    {
        $dte2 = [System.Runtime.InteropServices.Marshal]::GetActiveObject("VisualStudio.DTE." + $VSVersion)
        return $dte2
    }
    Catch
    {
        return $null
    }
}

function ExecuteCommand
{
    param(
        [Parameter(Mandatory=$true)]
        $dte2,
        [Parameter(Mandatory=$true)]
        [string]$command,
        [AllowEmptyString()]
        [Parameter(Mandatory=$true)]
        [string]$args,
        [Parameter(Mandatory=$true)]
        [string]$message,
        $waitTime = 0
    )

    Write-Host $message

    if ($args)
    {
        Write-Host 'Executing command ' $command ' with arguments: ' $args
        $dte2.ExecuteCommand($command, $args)
    }
    else
    {
        Write-Host 'Executing command ' $command ' without arguments'
        $dte2.ExecuteCommand($command)
    }


    # In examples like loading of a tool window, the window is not fully loaded when ExecuteCommand returns
    # So, the caller can choose to wait if needed. Not passing will result in no wait
    start-sleep $waitTime
}

function GetVSIXInstallerPath
{
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
        [string]$VSVersion
    )

    $VSIDEFolderPath = GetVSIDEFolderPath $VSVersion
    $VSIXInstallerPath = Join-Path $VSIDEFolderPath "VSIXInstaller.exe"

    return $VSIXInstallerPath
}

function UninstallVSIX
{
    param(
        [Parameter(Mandatory=$true)]
        [string]$vsixID,
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
        [string]$VSVersion,
        [Parameter(Mandatory=$true)]
        [int]$VSIXInstallerWaitTimeInSecs
    )

    $VSIXInstallerPath = GetVSIXInstallerPath $VSVersion

    Write-Host 'Uninstalling VSIX...'
    & $VSIXInstallerPath /q /a /u:$vsixID

    if ($lastexitcode)
    {
        Write-Error "Error uninstalling the VSIX! Exit code: $lastexitcode"
        return $false
    }

    start-sleep -Seconds $VSIXInstallerWaitTimeInSecs
    Write-Host "VSIX has been uninstalled successfully."
    
    return $true
}

function InstallVSIX
{
    param(
        [Parameter(Mandatory=$true)]
        [string]$vsixpath,
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
        [string]$VSVersion,
        [Parameter(Mandatory=$true)]
        [int]$VSIXInstallerWaitTimeInSecs
    )
    
    $VSIXInstallerPath = GetVSIXInstallerPath $VSVersion

    Write-Host "Installing VSIX from $vsixpath..."
    & $VSIXInstallerPath /q /a $vsixpath

    if ($lastexitcode)
    {
        Write-Error "Error installing the VSIX! Exit code: $lastexitcode"
        return $false
    }

    start-sleep -Seconds $VSIXInstallerWaitTimeInSecs
    Write-Host "VSIX has been installed successfully."

    return $true
}