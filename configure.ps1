<#
.SYNOPSIS
Configures NuGet.Client build environment. Detects and initializes
VS build toolsets. Configuration settings are stored at configure.json file.

.PARAMETER CleanCache
Cleans NuGet packages cache before build

.PARAMETER Force
Switch to force installation of required tools.

.PARAMETER CI
Indicates the build script is invoked from CI

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
    [switch]$CI,
    [Alias('s15')]
    [switch]$SkipVS15
)

. "$PSScriptRoot\build\common.ps1"

Trace-Log "Configuring NuGet.Client build environment"

$BuildErrors = @()

Invoke-BuildStep 'Configuring git repo' {
    Update-SubModules -Force:$Force
} -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' {
    Install-NuGet -Force:$Force
} -ev +BuildErrors

Invoke-BuildStep 'Installing .NET CLI' {
    Install-DotnetCLI -Force:$Force
} -ev +BuildErrors

Invoke-BuildStep 'Installing .NET CLI Test' {
    Install-DotnetCLI-Test -Force:$Force
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

    if (-not $ToolsetObject -and $ToolsetVersion -gt 14) {
        $VisualStudioPackageInstancesPath = "$env:ProgramData\Microsoft\VisualStudio\Packages\_Instances"

        if (Test-Path $VisualStudioPackageInstancesPath) {
            $WillowInstance = Get-ChildItem $VisualStudioPackageInstancesPath -filter state.json -recurse |
                sort LastWriteTime |
                select -last 1 |
                Get-Content -raw |
                ConvertFrom-Json

            if ($WillowInstance) {
                Verbose-Log "Using willow instance '$($WillowInstance.installationName)' installation path"
                $ToolsetObject = @{
                    VisualStudioInstallDir = [System.IO.Path]::GetFullPath((Join-Path $WillowInstance.installationPath Common7\IDE\))
                }
            }
        }
    }

    if (-not $ToolsetObject) {
        $DefaultInstallDir = Join-Path $env:ProgramFiles "Microsoft Visual Studio ${ToolsetVersion}.0\Common7\IDE\"
        if (Test-Path $DefaultInstallDir) {
            Verbose-Log "Using default location of Visual Studio installation path"
            $ToolsetObject = @{
                $VisualStudioInstallDir = $DefaultInstallDir
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

$MSBuildDefaultRoot = Join-Path $ProgramFiles MSBuild
$MSBuildRelativePath = 'bin\msbuild.exe'

Invoke-BuildStep 'Validating VS14 toolset installation' {
    $vs14 = New-BuildToolset 14
    if ($vs14) {
        $ConfigureObject.Toolsets.Add('vs14', $vs14)
        $script:MSBuildExe = Join-Path $MSBuildDefaultRoot "14.0\${MSBuildRelativePath}"
    }
} -ev +BuildErrors

Invoke-BuildStep 'Validating VS15 toolset installation' {
    $vs15 = New-BuildToolset 15
    if ($vs15) {
        $ConfigureObject.Toolsets.Add('vs15', $vs15)
        $WillowMSBuild = Join-Path $vs15.VisualStudioInstallDir ..\..\MSBuild
        $script:MSBuildExe = switch (Test-Path $WillowMSBuild) {
            $True { Join-Path $WillowMSBuild "15.0\${MSBuildRelativePath}" }
            $False { Join-Path $MSBuildDefaultRoot "15.0\${MSBuildRelativePath}" }
        }

        # Hack VSSDK path
        $VSToolsPath = Join-Path $MSBuildDefaultRoot 'Microsoft\VisualStudio\v15.0'
        $Targets = Join-Path $VSToolsPath 'VSSDK\Microsoft.VsSDK.targets'
        if (-not (Test-Path $Targets)) {
            Warning-Log "VSSDK is not found at default location '$VSToolsPath'. Attempting to override."
            # Attempting to fix VS SDK path for VS15 willow install builds
            # as MSBUILD failes to resolve it correctly
            $VSToolsPath = Join-Path $vs15.VisualStudioInstallDir '..\..\MSBuild\Microsoft\VisualStudio\v15.0' -Resolve
            $ConfigureObject.Add('EnvVars', @{ VSToolsPath = $VSToolsPath })
        }
    }
} -skip:($SkipVS15) -ev +BuildErrors

if ($MSBuildExe) {
    $MSBuildExe = [System.IO.Path]::GetFullPath($MSBuildExe)
    $MSBuildVersion = & $MSBuildExe '/version' '/nologo'
    Trace-Log "Using MSBUILD version $MSBuildVersion found at '$MSBuildExe'"
    $ConfigureObject.BuildTools.Add('MSBuildExe', $MSBuildExe)
}

New-Item $Artifacts -ItemType Directory -ea Ignore | Out-Null
$ConfigureObject | ConvertTo-Json | Set-Content $ConfigureJson

Trace-Log "Configuration data has been written to '$ConfigureJson'"

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}