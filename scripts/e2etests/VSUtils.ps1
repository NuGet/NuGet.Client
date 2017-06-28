$VSInstallerProcessName = "VSIXInstaller"

. "$PSScriptRoot\Utils.ps1"

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

    $VS15PreviewRelativePath = "Microsoft Visual Studio\Preview\Enterprise"
    $VS15StableRelativePath = "Microsoft Visual Studio\2017\Enterprise"

    if ($VSVersion -eq "15.0")
    {
        # Give preference to preview installation of VS2017
        if (Test-Path (Join-Path $ProgramFilesPath $VS15PreviewRelativePath))
        {
            $VSFolderPath = Join-Path $ProgramFilesPath $VS15PreviewRelativePath
        }
        elseif (Test-Path (Join-Path $ProgramFilesPath $VS15StableRelativePath))
        {
            $VSFolderPath = Join-Path $ProgramFilesPath $VS15StableRelativePath
        }
    }
    else
    {
        $VSFolderPath = Join-Path $ProgramFilesPath ("Microsoft Visual Studio " + $VSVersion)
    }
    
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
    $processName = 'devenv'
    Write-Host "Kill any running instances of $processName..."
    (Get-Process "$processName" -ErrorAction SilentlyContinue) | Kill -ErrorAction SilentlyContinue

    WaitForProcessExit -ProcessName "$processName" -TimeoutInSeconds 30
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
    $VSIXInstallerPath = Join-Path $VSIDEFolderPath "$VSInstallerProcessName.exe"

    return $VSIXInstallerPath
}

function GetDev15MEFCachePath
{
    $cachePath = $env:localappdata
    @( "Microsoft", "VisualStudio", "15.*", "ComponentModelCache" ) | %{ $cachePath = Join-Path $cachePath $_ }

    return $cachePath
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
        [int]$ProcessExitTimeoutInSeconds
    )

    $VSIXInstallerPath = GetVSIXInstallerPath $VSVersion

    Write-Host 'Uninstalling VSIX...'
    Write-Host "$VSIXInstallerPath" -Wait -PassThru -NoNewWindow -ArgumentList "/q /a /u:$vsixID"
    $p = start-process "$VSIXInstallerPath" -Wait -PassThru -NoNewWindow -ArgumentList "/q /a /u:$vsixID"

    if ($p.ExitCode -ne 0)
    {
        if ($p.ExitCode -eq 1002)
        {
            Write-Host "VSIX already uninstalled. Moving on to installing the VSIX! Exit code: $($p.ExitCode)"
            return $true
        }
        else
        {
            Write-Error "Error uninstalling the VSIX! Exit code: $($p.ExitCode)"
            return $false
        }
    }

    WaitForProcessExit -ProcessName $VSInstallerProcessName -TimeoutInSeconds $ProcessExitTimeoutInSeconds
    Write-Host "VSIX has been uninstalled successfully."

    return $true
}

function DowngradeVSIX
{
    param(
        [Parameter(Mandatory=$true)]
        [string]$vsixID,
        [Parameter(Mandatory=$true)]
        [ValidateSet("15.0")]
        [string]$VSVersion,
        [Parameter(Mandatory=$true)]
        [int]$ProcessExitTimeoutInSeconds
    )

    $VSIXInstallerPath = GetVSIXInstallerPath $VSVersion

    Write-Host 'Downgrading VSIX...'
    Write-Host "$VSIXInstallerPath" -Wait -PassThru -NoNewWindow -ArgumentList "/q /a /d:$vsixID"
    $p = start-process "$VSIXInstallerPath" -Wait -PassThru -NoNewWindow -ArgumentList "/q /a /d:$vsixID"

    if ($p.ExitCode -ne 0)
    {
        if ($p.ExitCode -eq 2001)
        {
            Write-Host "This VS2017 version does not support downgrade. Moving on to installing the VSIX! Exit code: $($p.ExitCode)" 
            return $true
        }
        else 
        {
            Write-Error "Error downgrading the VSIX! Exit code: $($p.ExitCode)"
            return $false
        }
    }

    WaitForProcessExit -ProcessName $VSInstallerProcessName -TimeoutInSeconds $ProcessExitTimeoutInSeconds
    Write-Host "VSIX has been downgraded successfully."

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
        [int]$ProcessExitTimeoutInSeconds
    )

    $VSIXInstallerPath = GetVSIXInstallerPath $VSVersion

    Write-Host "Installing VSIX from $vsixpath..."
    Write-Host "$VSIXInstallerPath" -Wait -PassThru -NoNewWindow -ArgumentList "/q $vsixpath"
    $p = start-process "$VSIXInstallerPath" -Wait -PassThru -NoNewWindow -ArgumentList "/q $vsixpath"

    if ($p.ExitCode -ne 0)
    {
        Write-Error "Error installing the VSIX! Exit code:  $($p.ExitCode)"
        return $false
    }

    WaitForProcessExit -ProcessName $VSInstallerProcessName -TimeoutInSeconds $ProcessExitTimeoutInSeconds
    Write-Host "VSIX has been installed successfully."

    return $true
}


function ClearDev15MEFCache
{
    $dev15MEFCachePath = GetDev15MEFCachePath

    Write-Host "rm -r $dev15MEFCachePath..."
    rm -r $dev15MEFCachePath
    Write-Host "Done clearing dev15 MEF cache..."
}