### Constants ###
$DefaultConfiguration = 'debug'
$DefaultReleaseLabel = 'zlocal'
$DefaultMSBuildVersion = 15
$DefaultVSVersion = "15.0"

# The pack version can be inferred from the .nuspec files on disk. This is only necessary as long
# as the following issue is open: https://github.com/NuGet/Home/issues/3530
$PackageReleaseVersion = "4.6.0"

$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$CLIRoot = Join-Path $NuGetClientRoot cli
$Artifacts = Join-Path $NuGetClientRoot artifacts
$Nupkgs = Join-Path $Artifacts nupkgs
$ReleaseNupkgs = Join-Path $Artifacts ReleaseNupkgs
$ConfigureJson = Join-Path $Artifacts configure.json
$VsWhereExe = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$VSVersion = $env:VisualStudioVersion
$DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
$NuGetExe = Join-Path $NuGetClientRoot '.nuget\nuget.exe'
$XunitConsole = Join-Path $NuGetClientRoot 'packages\xunit.runner.console.2.1.0\tools\xunit.console.exe'
$ILMerge = Join-Path $NuGetClientRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'

Set-Alias dotnet $DotNetExe
Set-Alias nuget $NuGetExe
Set-Alias xunit $XunitConsole
Set-Alias ilmerge $ILMerge

$Version = New-Object -TypeName System.Version -ArgumentList "4.0"

if ($PSVersionTable.PSVersion.CompareTo($Version) -lt 0) {
    Set-Alias wget Invoke-WebRequest
}

Function Read-PackageSources {
    param($NuGetConfig)
    $xml = New-Object xml
    $xml.Load($NuGetConfig)
    $xml.SelectNodes('/configuration/packageSources/add') | `
        ? { $_.key -ne "BuildFeed" } | `
        % { $_.value }
}
$PackageSources = Read-PackageSources (Join-Path $NuGetClientRoot 'NuGet.Config')

$OrigBgColor = $host.ui.rawui.BackgroundColor
$OrigFgColor = $host.ui.rawui.ForegroundColor

# MSBUILD has a nasty habit of leaving the foreground color red
Function Reset-Colors {
    $host.ui.rawui.BackgroundColor = $OrigBgColor
    $host.ui.rawui.ForegroundColor = $OrigFgColor
}

function Format-TeamCityMessage([string]$Text) {
    $Text.Replace("|", "||").Replace("'", "|'").Replace("[", "|[").Replace("]", "|]").Replace("`n", "|n").Replace("`r", "|r")
}

Function Trace-Log($TraceMessage = '') {
    Write-Host "[$(Trace-Time)]`t$TraceMessage" -ForegroundColor Cyan
}

Function Verbose-Log($VerboseMessage) {
    Write-Verbose "[$(Trace-Time)]`t$VerboseMessage"
}

Function Error-Log {
    param(
        [string]$ErrorMessage,
        [switch]$Fatal)
    if (-not $Fatal) {
        Write-Error "[$(Trace-Time)]`t$ErrorMessage"
    }
    else {
        Write-Error "[$(Trace-Time)]`t[FATAL] $ErrorMessage" -ErrorAction Stop
    }
}

Function Warning-Log($WarningMessage) {
    Write-Warning "[$(Trace-Time)]`t$WarningMessage"
}

Function Trace-Time() {
    $currentTime = Get-Date
    $lastTime = $Global:LastTraceTime
    $Global:LastTraceTime = $currentTime
    "{0:HH:mm:ss} +{1:F0}" -f $currentTime, ($currentTime - $lastTime).TotalSeconds
}

$Global:LastTraceTime = Get-Date

Function Format-ElapsedTime($ElapsedTime) {
    '{0:D2}:{1:D2}:{2:D2}' -f $ElapsedTime.Hours, $ElapsedTime.Minutes, $ElapsedTime.Seconds
}

Function Invoke-BuildStep {
    [CmdletBinding()]
    [Alias('ibs')]
    param(
        [Parameter(Mandatory=$True)]
        [string]$BuildStep,
        [Parameter(Mandatory=$True)]
        [ScriptBlock]$Expression,
        [Parameter(Mandatory=$False)]
        [Alias('args')]
        [Object[]]$Arguments,
        [Alias('skip')]
        [switch]$SkipExecution,
        [switch]$Critical
    )
    if (-not $SkipExecution) {
        if ($env:TEAMCITY_VERSION) {
            Write-Output "##teamcity[blockOpened name='$BuildStep']"
        }

        Trace-Log "[BEGIN] $BuildStep"
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $completed = $false
        $PwdBefore = $PWD

        try {
            if (-not $Arguments) {
                Invoke-Command $Expression -ErrorVariable err
            }
            else {
                Invoke-Command $Expression -ArgumentList $Arguments -ErrorVariable err
            }
            $completed = $true
        }
        finally {
            $sw.Stop()
            Reset-Colors
            if ($PWD -ne $PwdBefore) {
                cd $PwdBefore
            }
            if (-not $err -and $completed) {
                Trace-Log "[DONE +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
            }
            elseif (-not $err) {
                Trace-Log "[STOPPED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
            }
            else {
                Error-Log "[FAILED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
            }

            if ($env:TEAMCITY_VERSION) {
                Write-Output "##teamcity[blockClosed name='$BuildStep']"
            }
        }
    }
    else {
        Warning-Log "[SKIP] $BuildStep"
    }
}

Function Update-Submodules {
    [CmdletBinding()]
    param(
        [switch]$Force
    )
    $Submodules = Join-Path $NuGetClientRoot submodules -Resolve
    $GitAttributes = gci $Submodules\* -Filter '.gitattributes' -r -ea Ignore
    if ($Force -or -not $GitAttributes) {
        $opts = 'submodule', 'update'
        $opts += '--init'
        if (-not $VerbosePreference) {
            $opts += '--quiet'
        }

        Trace-Log 'Updating and initializing submodules'
        Trace-Log "git $opts"
        & git $opts 2>&1
    }
}

# Downloads NuGet.exe if missing
Function Install-NuGet {
    [CmdletBinding()]
    param(
        [switch]$Force
    )
    if ($Force -or -not (Test-Path $NuGetExe)) {
        Trace-Log 'Downloading nuget.exe'

        wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $NuGetExe
    }

    # Display nuget info
    & $NuGetExe locals all -list -verbosity detailed
}

Function Install-DotnetCLI {
    [CmdletBinding()]
    param(
        [switch]$Force
    )
    $MSBuildExe = Get-MSBuildExe
    $CliBranchForTesting = & $msbuildExe $NuGetClientRoot\build\config.props /v:m /nologo /t:GetCliBranchForTesting

    $cli = @{
        Root = $CLIRoot
        Version = 'latest'
        Channel = $CliBranchForTesting.Trim()
    }
    
    $DotNetExe = Join-Path $cli.Root 'dotnet.exe';

    if ([Environment]::Is64BitOperatingSystem) {
        $arch = "x64";
    }
    else {
        $arch = "x86";
    }

    $env:DOTNET_HOME=$cli.Root
    $env:DOTNET_INSTALL_DIR=$NuGetClientRoot

    if ($Force -or -not (Test-Path $DotNetExe)) {
        Trace-Log 'Downloading .NET CLI'

        New-Item -ItemType Directory -Force -Path $cli.Root | Out-Null

        $DotNetInstall = Join-Path $cli.Root 'dotnet-install.ps1'

        Invoke-WebRequest 'https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.ps1' -OutFile $DotNetInstall
        & $DotNetInstall -Channel $cli.Channel -i $cli.Root -Version $cli.Version -Architecture $arch
    }

    if (-not (Test-Path $DotNetExe)) {
        Error-Log "Unable to find dotnet.exe. The CLI install may have failed." -Fatal
    }

    # Display build info
    & $DotNetExe --info
}

Function Get-LatestVisualStudioRoot {
    # First try to use vswhere to find the latest version of Visual Studio.
    if (Test-Path $VsWhereExe) {
        $installationPath = & $VsWhereExe -latest -prerelease -property installationPath
        Verbose-Log "Found Visual Studio at '$installationPath' using vswhere"

        return $installationPath
    }

    $cachePath = "${Env:ProgramData}\Microsoft\VisualStudio\Packages"

    # Fall back to reading JSON directly (not recommended).
    'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\VisualStudio\Setup',
    'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\Setup',
    'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\Setup' | ForEach-Object {
        if (Test-Path $_) {
            $key = Get-ItemProperty $_

            if ($key.CachePath) {
                $cachePath = $key.CachePath
                return
            }
        }
    }

    $instances = Get-ChildItem "$cachePath\_Instances" -Filter state.json -Recurse | Get-Content | ConvertFrom-Json
    foreach ($instance in $instances) {
        if (-not $maxInstance) {
            $maxInstance = $instance
        } elseif ([Version]$instance.installationVersion -gt [Version]$maxInstance.installationVersion) {
            $maxInstance = $instance
        } elseif (([Version]$instance.installationVersion -eq [Version]$maxInstance.installationVersion) -and ([DateTime]$instance.installDate -gt [DateTime]$maxInstance.installDate)) {
            $maxInstance = $instance
        }
    }

    if ($maxInstance) {
        $installationPath = $maxInstance.installationPath
        Verbose-Log "Found Visual Studio at '$installationPath' using machine configuration"

        return $installationPath
    }

    Error-Log 'Cannot find an instance of Visual Studio 2017 or newer' -Fatal
}

Function Get-VSVersion() {
    if (-not $VSVersion) {
        $VSVersion = $DefaultVSVersion
    }
    return $VSVersion
}

Function Get-VSMajorVersion() {
    $vsVersion = Get-VSVersion
    $vsMajorVersion = "${vsVersion}".Split('.')[0]
    return $vsMajorVersion
}

Function Get-MSBuildRoot {
    param(
        [switch]$Default
    )

    $vsMajorVersion = Get-VSMajorVersion

    # Willow install workaround
    if (-not $Default) {

        # Find version 15.0 or newer
        $CommonToolsVar = "Env:VS${vsMajorVersion}0COMNTOOLS"
        if (Test-Path $CommonToolsVar) {
            # If VS "15" is installed get msbuild from VS install path
            $CommonToolsValue = gci $CommonToolsVar | select -expand value -ea Ignore
            $MSBuildRoot = Join-Path $CommonToolsValue '..\..\MSBuild' -Resolve
        } else {
            $VisualStudioRoot = Get-LatestVisualStudioRoot
            if ($VisualStudioRoot -and (Test-Path $VisualStudioRoot)) {
                $MSBuildRoot = Join-Path $VisualStudioRoot 'MSBuild'
            }
        }
    }

    # If not found before
    if (-not $MSBuildRoot -or -not (Test-Path $MSBuildRoot)) {
        # Assume msbuild is installed at default location
        $MSBuildRoot = Join-Path ${env:ProgramFiles(x86)} 'MSBuild'
    }

    $MSBuildRoot
}

Function Get-MSBuildExe {
    param(
        [int]$MSBuildVersion
    )
    # Get the highest msbuild version if version was not specified
    if (-not $MSBuildVersion) {
        return Get-MSBuildExe $DefaultMSBuildVersion
    }

    $MSBuildRoot = Get-MSBuildRoot
    $MSBuildExe = Join-Path $MSBuildRoot 'Current\bin\msbuild.exe'

    if (-not (Test-Path $MSBuildExe)) {
        $MSBuildExe = Join-Path $MSBuildRoot "${MSBuildVersion}.0\bin\msbuild.exe"
    }

    if (Test-Path $MSBuildExe) {
        Verbose-Log "Found MSBuild.exe at `"$MSBuildExe`""
        $MSBuildExe
    } else {
        Error-Log 'Could not find MSBuild.exe' -Fatal
    }
}

Function Test-MSBuildVersionPresent {
    [CmdletBinding()]
    param(
        [int]$MSBuildVersion = $DefaultMSBuildVersion
    )

    $MSBuildExe = Get-MSBuildExe $MSBuildVersion

    Test-Path $MSBuildExe
}

Function Test-BuildEnvironment {
    [CmdletBinding()]
    param(
        [switch]$CI
    )
    if (-not (Test-Path $ConfigureJson)) {
        # Run the configure script if it hasn't been executed
        $configureScriptPath = Join-Path $NuGetClientRoot configure.ps1
        Invoke-Expression $configureScriptPath
    }

    $Installed = (Test-Path $DotNetExe) -and (Test-Path $NuGetExe)
    if (-not $Installed) {
        Error-Log 'Build environment is not configured. Please run configure.ps1 first.' -Fatal
    }

    $script:ConfigureObject = Get-Content $ConfigureJson -Raw | ConvertFrom-Json
    Set-Variable MSBuildExe -Value $ConfigureObject.BuildTools.MSBuildExe -Scope Script -Force
    Set-Alias msbuild $script:MSBuildExe -Scope Script -Force
    Set-Variable BuildToolsets -Value $ConfigureObject.Toolsets -Scope Script -Force

    $script:VSToolsetInstalled = ($BuildToolsets | where vstoolset -ne $null)

    $ConfigureObject |
         select -expand envvars -ea Ignore |
         %{ $_.psobject.properties } |
         %{ Set-Item -Path "env:$($_.Name)" -Value $_.Value }

    if ($CI) {
        # Explicitly add cli to environment PATH
        # because dotnet-install script runs in configure.ps1 in previous build step
        $env:path = "$CLIRoot;${env:path}"
    }
}

Function Get-BuildNumber() {
    $NuGetEpoch = '2010-08-29T23:58:25-07:00' # NuGet client first commit!
    # Build number is a 16-bit integer. The limitation is imposed by VERSIONINFO.
    # https://msdn.microsoft.com/en-gb/library/aa381058.aspx
    [uint16]((((Get-Date) - (Get-Date $NuGetEpoch)).TotalMinutes / 5) % [uint16]::MaxValue)
}

Function Clear-PackageCache {
    [CmdletBinding()]
    param()
    Trace-Log 'Cleaning local caches'

    & nuget locals all -clear -verbosity detailed
}

Function Clear-Artifacts {
    [CmdletBinding()]
    param()
    if( Test-Path $Artifacts) {
        Trace-Log 'Cleaning the Artifacts folder'
        Remove-Item $Artifacts\* -Recurse -Force -Exclude 'configure.json'
    }
}

Function Clear-Nupkgs {
    [CmdletBinding()]
    param()
    if (Test-Path $Nupkgs) {
        Trace-Log 'Cleaning nupkgs folder'
        Remove-Item $Nupkgs\*.nupkg -Force
    }
}

Function Restore-SolutionPackages{
    [CmdletBinding()]
    param(
        [Alias('path')]
        [string]$SolutionPath,
        [ValidateSet(15)]
        [int]$MSBuildVersion
    )
    $opts = , 'restore'
    if (-not $SolutionPath) {
        $opts += "${NuGetClientRoot}\.nuget\packages.config", '-SolutionDirectory', $NuGetClientRoot
    }
    else {
        $opts += $SolutionPath
    }

    if ($MSBuildVersion) {
        $opts += '-MSBuildVersion', $MSBuildVersion
    }

    if (-not $VerbosePreference) {
        $opts += '-verbosity', 'quiet'
    }

    Trace-Log "Restoring packages @""$NuGetClientRoot"""
    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Restore failed @""$NuGetClientRoot"". Code: ${LASTEXITCODE}"
    }
}
