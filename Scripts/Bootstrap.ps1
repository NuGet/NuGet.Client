param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [string]$FuncTestRoot)

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

function ExtractEndToEndZip
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [string]$FuncTestRoot,
    [Parameter(Mandatory=$true)]
    [string]$NuGetTestPath)

    $endToEndZipSrc = Join-Path $NuGetDropPath 'EndToEnd.zip'
    $endToEndZip = Join-Path $FuncTestRoot 'EndToEnd.zip'

    Copy-Item $endToEndZipSrc $endToEndZip -Force

    Write-Host 'Creating ' $NuGetTestPath
    mkdir $NuGetTestPath

    ExtractZip $endToEndZip $NuGetTestPath
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

Write-Host "This script will clean, copy and extract EndToEnd.zip file"
Write-Host "EndtoEnd.zip file from the CI all the necessary scripts to run the functional tests"

Write-Host 'Try to kill any running instances of devenv to be able to cleanup EndToEnd folder'
$pcs = (Get-Process 'devenv' -ErrorAction SilentlyContinue)
$pcs | Kill -ErrorAction SilentlyContinue
if ($pcs.Count -gt 0)
{
    Write-Host 'Since VS has been killed, wait for 3 seconds to be able to EndToEnd folder'
    Start-Sleep 3
}

$NuGetTestPath = Join-Path $FuncTestRoot "EndToEnd"

Write-Host "NuGet drop path is $NuGetDropPath"
Write-Host "NuGet test path is $NuGetTestPath"

CleanPaths $NuGetTestPath
ExtractEndToEndZip $NuGetDropPath $FuncTestRoot $NuGetTestPath

Write-Host "Cleaned, Copied and Extracted and EndToEnd.zip file"