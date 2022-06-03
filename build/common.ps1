### Constants ###
$DefaultMSBuildVersion = 15

$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$CLIRoot = Join-Path $NuGetClientRoot cli
$CLIRootForPack = Join-Path $NuGetClientRoot "cli1.0.4"
$Artifacts = Join-Path $NuGetClientRoot artifacts
$Nupkgs = Join-Path $Artifacts nupkgs
$ConfigureJson = Join-Path $Artifacts configure.json

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
        [switch]$Force,
        [switch]$CI
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
    $msbuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\bin\msbuild.exe'
    $CliTargetBranch = & $msbuildExe $NuGetClientRoot\build\config.props /v:m /nologo /t:GetCliTargetBranch

    $cli = @{
            Root = $CLIRoot
            DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
            DotNetInstallUrl = 'https://raw.githubusercontent.com/dotnet/cli/4bd9bb92cc3636421cd01baedbd8ef3e41aa1e22/scripts/obtain/dotnet-install.ps1'
        }

    $env:DOTNET_HOME=$cli.Root
    $env:DOTNET_INSTALL_DIR=$NuGetClientRoot

    if ($Force -or -not (Test-Path $cli.DotNetExe)) {
        Trace-Log 'Downloading .NET CLI'

        New-Item -ItemType Directory -Force -Path $cli.Root | Out-Null

        $DotNetInstall = Join-Path $cli.Root 'dotnet-install.ps1'

        Invoke-WebRequest $cli.DotNetInstallUrl -OutFile $DotNetInstall
        $channel = $CliTargetBranch.Trim()
        & $DotNetInstall -Channel $channel  -i $cli.Root
    }

    if (-not (Test-Path $cli.DotNetExe)) {
        Error-Log "Unable to find dotnet.exe. The CLI install may have failed." -Fatal
    }

    # Display build info
    & $cli.DotNetExe --info
}

Function Install-DotnetCLIToILMergePack {
    [CmdletBinding()]
    param(
        [switch]$Force
    )

    $cli = @{
            Root = $CLIRootForPack
            DotNetExe = Join-Path $CLIRootForPack 'dotnet.exe'
            DotNetInstallUrl = 'https://raw.githubusercontent.com/dotnet/cli/58b0566d9ac399f5fa973315c6827a040b7aae1f/scripts/obtain/dotnet-install.ps1'
            Version = '1.0.1'
        }

    if ([Environment]::Is64BitOperatingSystem) {
        $arch = "x64";
    }
    else {
        $arch = "x86";
    }

    $env:DOTNET_HOME=$cli.Root
    $env:DOTNET_INSTALL_DIR=$NuGetClientRoot

    if ($Force -or -not (Test-Path $cli.DotNetExe)) {
        Trace-Log 'Downloading .NET CLI'

        New-Item -ItemType Directory -Force -Path $cli.Root | Out-Null

        $DotNetInstall = Join-Path $cli.Root 'dotnet-install.ps1'

        Invoke-WebRequest $cli.DotNetInstallUrl -OutFile $DotNetInstall

        & $DotNetInstall -Channel preview -i $cli.Root -Version $cli.Version -Architecture $arch
    }

    if (-not (Test-Path $cli.DotNetExe)) {
        Error-Log "Unable to find dotnet.exe. The CLI install may have failed." -Fatal
    }

    # Display build info
    & $cli.DotNetExe --info
}

Function Get-MSBuildRoot {
    param(
        [ValidateSet(14,15)]
        [int]$MSBuildVersion,
        [switch]$Default
    )
    # Willow install workaround
    if (-not $Default -and $MSBuildVersion -eq 15 -and (Test-Path Env:\VS150COMNTOOLS)) {
        # If VS "15" is installed get msbuild from VS install path
        $MSBuildRoot = Join-Path $env:VS150COMNTOOLS ..\..\MSBuild
    }

    # If not found before
    if (-not $MSBuildRoot -or -not (Test-Path $MSBuildRoot)) {
        # Assume msbuild is installed at default location
        $MSBuildRoot = Join-Path ${env:ProgramFiles(x86)} MSBuild
    }

    $MSBuildRoot
}

Function Get-MSBuildExe {
    param(
        [ValidateSet(15)]
        [int]$MSBuildVersion
    )
    # Get the highest msbuild version if version was not specified
    if (-not $MSBuildVersion) {
        return Get-MSBuildExe 15
    }

    $MSBuildRoot = Get-MSBuildRoot $MSBuildVersion
    Join-Path $MSBuildRoot "${MSBuildVersion}.0\bin\msbuild.exe"
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

    $script:VS14Installed = ($BuildToolsets | where vs14 -ne $null)
    $script:VS15Installed = ($BuildToolsets | where vs15 -ne $null)

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
    $SemanticVersionDate = '2018-05-30' # Date format - yyyy-mm-dd
    try {
        [uint16](((Get-Date) - (Get-Date $SemanticVersionDate)).TotalMinutes / 5)
    }
    catch {
        # Build number is a 16-bit integer. The limitation is imposed by VERSIONINFO.
        # https://msdn.microsoft.com/en-gb/library/aa381058.aspx
        Error-Log "Build number is out of range! Consider advancing SemanticVersionDate in common.ps1." -Fatal
    }
}

Function Format-BuildNumber([int]$BuildNumber) {
    '{0:D4}' -f $BuildNumber
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