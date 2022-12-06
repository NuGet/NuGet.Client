### Constants ###
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$CLIRoot = Join-Path $NuGetClientRoot cli
$Artifacts = Join-Path $NuGetClientRoot artifacts
$Nupkgs = Join-Path $Artifacts nupkgs
$ConfigureJson = Join-Path $Artifacts configure.json
$BuiltInVsWhereExe = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$VSVersion = $env:VisualStudioVersion
$DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
$ILMerge = Join-Path $NuGetClientRoot 'packages\ilmerge\2.14.1208\tools\ILMerge.exe'

Set-Alias dotnet $DotNetExe
Set-Alias ilmerge $ILMerge

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

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
        [Parameter(Mandatory = $True)]
        [string]$BuildStep,
        [Parameter(Mandatory = $True)]
        [ScriptBlock]$Expression,
        [Parameter(Mandatory = $False)]
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

Function Install-DotnetCLI {
    [CmdletBinding()]
    param(
        [switch]$Force
    )
    $vsMajorVersion = Get-VSMajorVersion
    $MSBuildExe = Get-MSBuildExe $vsMajorVersion
    $CliBranchListForTesting = & $msbuildExe $NuGetClientRoot\build\config.props /v:m /nologo /t:GetCliBranchForTesting
    $CliBranchList = $CliBranchListForTesting.Split(';');

    $DotNetInstall = Join-Path $CLIRoot 'dotnet-install.ps1'

    #If "-force" is specified, or dotnet.exe under cli folder doesn't exist, create cli folder and download dotnet-install.ps1 into cli folder.
    if ($Force -or -not (Test-Path $DotNetExe)) {
        Trace-Log "Downloading .NET CLI $CliBranchListForTesting"

        New-Item -ItemType Directory -Force -Path $CLIRoot | Out-Null

        Invoke-WebRequest 'https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.ps1' -OutFile $DotNetInstall
    }

    ForEach ($CliBranch in $CliBranchList) {
        $CliBranch = $CliBranch.trim()
        $CliChannelAndVersion = $CliBranch -split "\s+"

        $Channel = $CliChannelAndVersion[0].trim()
        if ($CliChannelAndVersion.count -eq 1) {
            $Version = 'latest'
        }
        else {
            $Version = $CliChannelAndVersion[1].trim()
        }

        $cli = @{
            Root    = $CLIRoot
            Version = $Version
            Channel = $Channel
        }
    
        $DotNetExe = Join-Path $cli.Root 'dotnet.exe';

        if ([Environment]::Is64BitOperatingSystem) {
            $arch = "x64";
        }
        else {
            $arch = "x86";
        }

        $env:DOTNET_HOME = $cli.Root
        $env:DOTNET_INSTALL_DIR = $NuGetClientRoot

        if ($Version -eq 'latest') {
            #Get the latest specific version number for a certain channel from url like : https://dotnetcli.blob.core.windows.net/dotnet/Sdk/release/3.0.1xx/latest.version"
            $httpGetUrl = "https://dotnetcli.blob.core.windows.net/dotnet/Sdk/" + $Channel + "/latest.version"
            $versionFile = Invoke-RestMethod -Method Get -Uri $httpGetUrl

            $stringReader = New-Object -TypeName System.IO.StringReader -ArgumentList $versionFile
            
            [int]$count = 0
            while ( $line = $stringReader.ReadLine() ) {
                if ($count -eq 0) {
                    $specificVersion = $line.trim()
                }
                $count += 1
            }
        }
        else {
            $specificVersion = $Version
        }
        
        Trace-Log "The version of SDK should be installed is : $specificVersion"

        $probeDotnetPath = Join-Path (Join-Path $cli.Root sdk)  $specificVersion

        Trace-Log "Probing folder : $probeDotnetPath"

        #If "-force" is specified, or folder with specific version doesn't exist, the download command will run" 
        if ($Force -or -not (Test-Path $probeDotnetPath)) {
            & $DotNetInstall -Channel $cli.Channel -i $cli.Root -Version $cli.Version -Architecture $arch -NoPath
        }

        if (-not (Test-Path $DotNetExe)) {
            Error-Log "Unable to find dotnet.exe. The CLI install may have failed." -Fatal
        }
        if (-not(Test-Path $probeDotnetPath)) {
            Error-Log "Unable to find specific version of sdk. The CLI install may have failed." -Fatal
        }

        # Display build info
        & $DotNetExe --info
    }

    # Install the 2.x runtime because our tests target netcoreapp2x
    Trace-Log "$DotNetInstall -Runtime dotnet -Channel 2.2 -i $CLIRoot -NoPath"
    & $DotNetInstall -Runtime dotnet -Channel 2.2 -i $CLIRoot -NoPath

    # Install the 5.x runtime because our tests target netcoreapp5x
    Trace-Log "$DotNetInstall -Runtime dotnet -Channel 5.0 -i $CLIRoot -NoPath"
    & $DotNetInstall -Runtime dotnet -Channel 5.0 -i $CLIRoot -NoPath

    # Display build info
    & $DotNetExe --info
}

Function Get-LatestVisualStudioRoot {

    if (Test-Path $BuiltInVsWhereExe) {
        $installationPath = & $BuiltInVsWhereExe -latest -prerelease -property installationPath
        $installationVersion = & $BuiltInVsWhereExe -latest -prerelease -property installationVersion
        Verbose-Log "Found Visual Studio at '$installationPath' version '$installationVersion' with '$BuiltInVsWhereExe'"
        # Set the fallback version
        $majorVersion = "$installationVersion".Split('.')[0]
        $script:FallbackVSVersion = "$majorVersion.0"

        return $installationPath
    }

    Error-Log "Could not find a compatible Visual Studio Version because $BuiltInVsWhereExe does not exist" -Fatal
}

<#
.DESCRIPTION
Finds a suitable VSVersion based on the environment configuration,
if $VSVersion is set, that means we're running in a developer command prompt so we prefer that.
otherwise we pick the latest Visual Studio version available on the machine.
#>
Function Get-VSVersion() {
    if (-not $VSVersion) {
        if (-not $script:FallbackVSVersion) {
            Verbose-Log "No fallback VS Version set yet. This means that we are running outside of a developer command prompt scope."
            $_ = Get-LatestVisualStudioRoot
        }
        Verbose-Log "Using the fallback VS version '$script:FallbackVSVersion'"
        $VSVersion = $script:FallbackVSVersion
    }
    return $VSVersion
}

Function Get-VSMajorVersion() {
    $vsVersion = Get-VSVersion
    $vsMajorVersion = "${vsVersion}".Split('.')[0]
    return $vsMajorVersion
}

Function Get-MSBuildExe {
    param(
        [ValidateSet("15", "16", $null)]
        [string]$MSBuildVersion
    )

    if (-not $MSBuildVersion) {
        $MSBuildVersion = Get-VSMajorVersion
    }

    $CommonToolsVar = "Env:VS${MSBuildVersion}0COMNTOOLS"
    if (Test-Path $CommonToolsVar) {
        $CommonToolsValue = gci $CommonToolsVar | select -expand value -ea Ignore
        $MSBuildRoot = Join-Path $CommonToolsValue '..\..\MSBuild' -Resolve
    }
    else {
        $VisualStudioRoot = Get-LatestVisualStudioRoot
        if ($VisualStudioRoot -and (Test-Path $VisualStudioRoot)) {
            $MSBuildRoot = Join-Path $VisualStudioRoot 'MSBuild'
        }
    }

    $MSBuildExe = Join-Path $MSBuildRoot 'Current\bin\msbuild.exe'

    if (-not (Test-Path $MSBuildExe)) {
        $MSBuildExe = Join-Path $MSBuildRoot "${MSBuildVersion}.0\bin\msbuild.exe"
    }

    if (Test-Path $MSBuildExe) {
        Verbose-Log "Found MSBuild.exe at `"$MSBuildExe`""
        $MSBuildExe
    }
    else {
        Error-Log 'Could not find MSBuild.exe' -Fatal
    }
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

    $Installed = (Test-Path $DotNetExe)
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
    % { $_.psobject.properties } |
    % { Set-Item -Path "env:$($_.Name)" -Value $_.Value }

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

    & dotnet nuget locals all --clear
}

Function Clear-Artifacts {
    [CmdletBinding()]
    param()
    if ( Test-Path $Artifacts) {
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

Function Restore-SolutionPackages {
    [CmdletBinding()]
    param(
    )
    $opts = 'msbuild', '-t:restore'
    $opts += "${NuGetClientRoot}\build\bootstrap.proj"

    Trace-Log "Restoring packages @""$NuGetClientRoot"""
    Trace-Log "dotnet $opts"
    & dotnet $opts
    if (-not $?) {
        Error-Log "Restore failed @""$NuGetClientRoot"". Code: ${LASTEXITCODE}"
    }
}
