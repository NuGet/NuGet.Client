<#
.SYNOPSIS
Configures NuGet.Client build environment.

.PARAMETER ToolsetVersion
Sets environment variables relevant to the desired VS toolset.

.PARAMETER CleanCache
Cleans NuGet packages cache before build

.PARAMETER Force
Switch to force installation of required tools.

.PARAMETER CI
Indicates the build script is invoked from CI

.EXAMPLE
Clean install using VS14 (default) toolset
.\configure.ps1 -cc -v

Incremental install for VS15 toolset
.\configure.ps1 -tv 15 -v
#>
[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [Alias('tv')]
    [int]$ToolsetVersion = 14,
    [Alias('cc')]
    [switch]$CleanCache,
    [Alias('f')]
    [switch]$Force,
    [switch]$CI
)

. "$PSScriptRoot\build\common.ps1"

Trace-Log "Configuring NuGet.Client build environment for VS${ToolsetVersion} Toolset"

Update-SubModules -Force:$Force

Install-NuGet -Force:$Force

Install-DotnetCLI -Force:$Force

if ($CleanCache) {
    Clear-PackageCache
}

Trace-Log "Validating VS${ToolsetVersion} toolset installation"

$CommonToolsVar = "Env:VS${ToolsetVersion}0COMNTOOLS"
if (Test-Path $CommonToolsVar) {
    $CommonToolsValue = gci $CommonToolsVar | select -expand value -ea Ignore
    Verbose-Log "Using environment variable `"$CommonToolsVar`" = `"$CommonToolsValue`""
    $VisualStudioInstallDir = [System.IO.Path]::GetFullPath((Join-Path $CommonToolsValue '..\IDE'))
}
else {
    $VisualStudioRegistryKey = "HKCU:\SOFTWARE\Microsoft\VisualStudio\${ToolsetVersion}.0_Config"
    Verbose-Log "Retrieving Visual Studio installation path from registry '$VisualStudioRegistryKey'"

    $VisualStudioInstallDir = gp $VisualStudioRegistryKey | select -expand InstallDir -ea Ignore
    if (-not $VisualStudioInstallDir) {
        Verbose-Log "Using default location of Visual Studio installation path"
        $VisualStudioInstallDir = "${env:ProgramFiles}\Microsoft Visual Studio ${ToolsetVersion}.0\Common7\IDE\"
    }
}

Resolve-Path $VisualStudioInstallDir | Out-Null

Verbose-Log "VisualInstallDir = '$VisualStudioInstallDir'"

if ($ToolsetVersion -eq 15) {
    $MSBuildDefaultRoot = Get-MSBuildRoot 15 -Default
    $VSToolsPath = Join-Path $MSBuildDefaultRoot 'Microsoft\VisualStudio\v15.0'
    $Targets = Join-Path $VSToolsPath 'VSSDK\Microsoft.VsSDK.targets'
    if (-not (Test-Path $Targets)) {
        Warning-Log "VSSDK is not found at default location '$VSToolsPath'. Attempting to override."
        # Attempting to fix VS SDK path for VS15 willow install builds
        # as MSBUILD failes to resolve it correctly
        $env:VSToolsPath = Join-Path $VisualStudioInstallDir '..\..\MSBuild\Microsoft\VisualStudio\v15.0' -Resolve
        Trace-Log "VSToolsPath now is '${env:VSToolsPath}'"
    }
}
