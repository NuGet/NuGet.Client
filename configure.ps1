<#
.SYNOPSIS
Configures NuGet.Client build environment. Detects and initializes
VS build toolsets. Configuration settings are stored at configure.json file.

.PARAMETER CleanCache
Cleans NuGet packages cache before build

.PARAMETER Force
Switch to force installation of required tools.

.PARAMETER Test
Indicates the Tests need to be run. Downloads the Test cli when tests are needed to run.

.EXAMPLE
.\configure.ps1 -cc -v
Clean repo build environment configuration

.EXAMPLE
.\configure.ps1 -v
Incremental install of build tools
#>
[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [Alias('cc')]
    [switch]$CleanCache,
    [Alias('f')]
    [switch]$Force,
    [switch]$RunTest
)

. "$PSScriptRoot\build\common.ps1"

Trace-Log "Configuring NuGet.Client build environment"

$BuildErrors = @()

Invoke-BuildStep 'Configuring git repo' {
    Update-SubModules -Force:$Force
} -ev +BuildErrors

Invoke-BuildStep 'Installing .NET CLI' {
    Install-DotnetCLI -Force:$Force   
} -ev +BuildErrors

# Restoring tools required for build
Invoke-BuildStep 'Restoring solution packages' {
    Restore-SolutionPackages
} -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' {
    Clear-PackageCache
} -skip:(-not $CleanCache) -ev +BuildErrors

$ConfigureObject = @{
    BuildTools = @{}
    Toolsets = @{}
}

Function New-BuildToolset {
    param(
        [ValidateSet(15, 16)]
        [int]$ToolsetVersion
    )
    $CommonToolsVar = "Env:VS${ToolsetVersion}0COMNTOOLS"
    if (Test-Path $CommonToolsVar) {
        $CommonToolsValue = gci $CommonToolsVar | select -expand value -ea Ignore
        Verbose-Log "Using environment variable `"$CommonToolsVar`" = `"$CommonToolsValue`""
        $ToolsetObject = @{
            VisualStudioInstallDir = [System.IO.Path]::GetFullPath((Join-Path $CommonToolsValue '..\IDE'))
        }
    }

    if (-not $ToolsetObject) {
        $VisualStudioRegistryKey = "HKCU:\SOFTWARE\Microsoft\VisualStudio\${ToolsetVersion}.0_Config"
        if (Test-Path $VisualStudioRegistryKey) {
            Verbose-Log "Retrieving Visual Studio installation path from registry '$VisualStudioRegistryKey'"
            $ToolsetObject = @{
                VisualStudioInstallDir = gp $VisualStudioRegistryKey | select -expand InstallDir -ea Ignore
            }
        }
    }

    if (-not $ToolsetObject) {
        $VisualStudioInstallRootDir = Get-LatestVisualStudioRoot

        if ($VisualStudioInstallRootDir) {
            Verbose-Log "Using willow instance '$VisualStudioInstallRootDir' installation path"
            $ToolsetObject = @{
                VisualStudioInstallDir = [System.IO.Path]::GetFullPath((Join-Path $VisualStudioInstallRootDir Common7\IDE\))
            }
        }
    }

    if (-not $ToolsetObject) {
        $DefaultInstallDir = Join-Path $env:ProgramFiles "Microsoft Visual Studio ${ToolsetVersion}.0\Common7\IDE\"
        if (Test-Path $DefaultInstallDir) {
            Verbose-Log "Using default location of Visual Studio installation path"
            $ToolsetObject = @{
                VisualStudioInstallDir = $DefaultInstallDir
            }
        }
    }

    if (-not $ToolsetObject) {
        Warning-Log "Toolset VS${ToolsetVersion} is not found."
    }

    # return toolset build configuration object
    $ToolsetObject
}

$ProgramFiles = ${env:ProgramFiles(x86)}

if (-not $ProgramFiles -or -not (Test-Path $ProgramFiles)) {
    $ProgramFiles = $env:ProgramFiles
}

$vsMajorVersion = Get-VSMajorVersion
$validateToolsetMessage = "Validating VS $vsMajorVersion toolset installation" 

Invoke-BuildStep $validateToolsetMessage {

    $vstoolset = New-BuildToolset $vsMajorVersion
    if ($vstoolset) {
        $ConfigureObject.Toolsets.Add('vstoolset', $vstoolset)
        $script:MSBuildExe = Get-MSBuildExe $vsMajorVersion
    }
} -ev +BuildErrors

if ($MSBuildExe) {
    $MSBuildExe = [System.IO.Path]::GetFullPath($MSBuildExe)
    $MSBuildVersion = & $MSBuildExe '/version' '/nologo'
    Trace-Log "Using MSBUILD version $MSBuildVersion found at '$MSBuildExe'"
    $ConfigureObject.BuildTools.Add('MSBuildExe', $MSBuildExe)
}

New-Item $Artifacts -ItemType Directory -ea Ignore | Out-Null
$ConfigureObject | ConvertTo-Json -Compress | Set-Content $ConfigureJson

Trace-Log "Configuration data has been written to '$ConfigureJson'"

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}