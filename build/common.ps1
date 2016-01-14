### Constants ###
$ValidConfigurations = 'debug', 'release'
$DefaultConfiguration = 'debug'
$ValidReleaseLabels = 'Release','rtm', 'rc', 'beta', 'local'
$DefaultReleaseLabel = 'local'

$DefaultDnxVersion = '1.0.0-rc1-update1'
$DefaultDnxArch = 'x86'
$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$MSBuildExe = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\msbuild.exe'
$NuGetExe = Join-Path $NuGetClientRoot '.nuget\nuget.exe'
$ILMerge = Join-Path $NuGetClientRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'
$DnvmCmd = Join-Path $env:USERPROFILE '.dnx\bin\dnvm.cmd'
$Nupkgs = Join-Path $NuGetClientRoot nupkgs
$Artifacts = Join-Path $NuGetClientRoot artifacts

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

### Functions ###
Function Trace-Log($TraceMessage = '') {
    Write-Host "[$(Trace-Time)]`t$TraceMessage" -ForegroundColor Cyan
}

Function Verbose-Log($VerboseMessage) {
    Write-Verbose "[$(Trace-Time)]`t$VerboseMessage"
}

Function Error-Log($ErrorMessage) {
    Write-Error "[$(Trace-Time)]`t$ErrorMessage"
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
    '{0:F0}:{1:D2}' -f $ElapsedTime.TotalMinutes, $ElapsedTime.Seconds
}

Function Invoke-BuildStep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True)]
        [string]$BuildStep,
        [Parameter(Mandatory=$True)]
        [ScriptBlock]$Expression,
        [Parameter(Mandatory=$False)]
        [Alias('args')]
        [Object[]]$Arguments,
        [Alias('skip')]
        [switch]$SkipExecution
    )
    if (-not $SkipExecution) {
        Trace-Log "[BEGIN] $BuildStep"
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $completed = $false
        try {
            Invoke-Command $Expression -ArgumentList $Arguments -ErrorVariable err
            $completed = $true
        }
        finally {
            $sw.Stop()
            Reset-Colors
            if ($completed) {
                Trace-Log "[DONE +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
            }
            else {
                if (-not $err) {
                    Trace-Log "[STOPPED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
                }
                else {
                    Error-Log "[FAILED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
                }
            }
        }
    }
    else {
        Warning-Log "[SKIP] $BuildStep"
    }
}

Function Update-Submodules {
    [CmdletBinding()]
    param()
    $opts = 'submodule', 'update'
    if (-not (Test-Path -Path "$NuGetClientRoot/submodules/FileSystem/src")) {
        Trace-Log 'Updating and initializing submodules'
        $opts += '--init'
    }
    if (-not $VerbosePreference) {
        $opts += '--quiet'
    }
    Verbose-Log "git $opts"
    & git $opts 2>&1
}

# Downloads NuGet.exe if missing
Function Install-NuGet {
    [CmdletBinding()]
    param()
    if (-not (Test-Path $NuGetExe)) {
        Trace-Log 'Downloading nuget.exe'
        wget https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe -OutFile $NuGetExe
    }
}

# Validates DNVM installed and installs it if missing
Function Install-DNVM {
    [CmdletBinding()]
    param()
    if (-not (Test-Path $DnvmCmd)) {
        Trace-Log 'Downloading DNVM'
        &{
            $Branch='dev'
            iex (`
                (new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1')`
            )
        }
    }
}

# Makes sure the needed DNX runtimes installed
Function Install-DNX {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True, Position=1)]
        [Alias('r')]
        [ValidateSet('CLR', 'CoreCLR')]
        [string]$Runtime,
        [Alias('v')]
        [string]$Version = $DefaultDnxVersion,
        [Alias('a')]
        [string]$Arch = $DefaultDnxArch,
        [switch]$Default
    )
    Install-DNVM
    $env:DNX_FEED = 'https://www.nuget.org/api/v2'
    $opts = $Version, '-runtime', $Runtime, '-arch', $Arch
    if ($Default) {
        $opts += '-alias', 'default'
    }
    Verbose-Log "dnvm install $opts"
    & dnvm install $opts 2>&1
}

Function Use-DNX {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True, Position=1)]
        [Alias('r')]
        [ValidateSet('CLR', 'CoreCLR')]
        [string]$Runtime,
        [Alias('v')]
        [string]$Version = $DefaultDnxVersion,
        [Alias('a')]
        [string]$Arch = $DefaultDnxArch
    )
    $opts = $Version, '-runtime', $Runtime, '-arch', $Arch
    Verbose-Log "dnvm use $opts"
    & dnvm use $opts 2>&1
}

# Enables delay signed build
Function Enable-DelaySigning {
    [CmdletBinding()]
    param($MSPFXPath, $NuGetPFXPath)
    if (Test-Path $MSPFXPath) {
        Trace-Log "Setting NuGet.Core solution to delay sign using $MSPFXPath"
        $env:DNX_BUILD_KEY_FILE=$MSPFXPath
        $env:DNX_BUILD_DELAY_SIGN=$true
    }

    if (Test-Path $NuGetPFXPath) {
        Trace-Log "Setting NuGet.Clients solution to delay sign using $NuGetPFXPath"
        $env:NUGET_PFX_PATH= $NuGetPFXPath

        Trace-Log "Using the Microsoft Key for NuGet Command line $MSPFXPath"
        $env:MS_PFX_PATH=$MSPFXPath
    }
}

Function Get-BuildNumber() {
    $SemanticVersionDate = '2015-11-30'
    [int](((Get-Date) - (Get-Date $SemanticVersionDate)).TotalMinutes / 5)
}

Function Format-BuildNumber([int]$BuildNumber) {
    '{0:D4}' -f $BuildNumber
}

## Cleans the machine level cache from all packages
Function Clear-PackageCache {
    [CmdletBinding()]
    param()
    Trace-Log 'Removing DNX packages'

    if (Test-Path $env:userprofile\.dnx\packages) {
        rm -r $env:userprofile\.dnx\packages -Force
    }

    Trace-Log 'Removing .NUGET packages'

    if (Test-Path $env:userprofile\.nuget\packages) {
        rm -r $env:userprofile\.nuget\packages -Force
    }

    Trace-Log 'Removing DNU cache'

    if (Test-Path $env:localappdata\dnu\cache) {
        rm -r $env:localappdata\dnu\cache -Force
    }

    Trace-Log 'Removing NuGet web cache'

    if (Test-Path $env:localappdata\NuGet\v3-cache) {
        rm -r $env:localappdata\NuGet\v3-cache -Force
    }

    Trace-Log 'Removing NuGet machine cache'

    if (Test-Path $env:localappdata\NuGet\Cache) {
        rm -r $env:localappdata\NuGet\Cache -Force
    }
}

Function Clear-Artifacts {
    [CmdletBinding()]
    param()
    if( Test-Path $Artifacts) {
        Trace-Log 'Cleaning the Artifacts folder'
        Remove-Item $Artifacts\*.* -Recurse -Force
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
        [ValidateSet(4, 12, 14)]
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
        $opts += '-Verbosity', 'quiet'
    }

    Trace-Log "Restoring packages @""$NuGetClientRoot"""
    Verbose-Log "$NuGetExe $opts"
    & $NuGetExe $opts 2>&1
    if (-not $?) {
        Error-Log "Restore failed @""$NuGetClientRoot"". Code: ${LASTEXITCODE}"
    }
}

# Restore projects individually
Function Restore-XProject {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True)]
        [string]$XProjectLocation,
        [switch]$V2
    )
    Process {
        $projectJsonFile = Join-Path $XProjectLocation 'project.json'
        $opts = 'restore', $projectJsonFile
        $opts += $PackageSources | %{ '-s', $_ }
        if (-not $VerbosePreference) {
            $opts += '--quiet'
        }

        Trace-Log "Restoring packages @""$XProjectLocation"""
        Verbose-Log "dnu $opts"
        & dnu $opts 2>&1
        if (-not $?) {
            Error-Log "Restore failed @""$XProjectLocation"". Code: $LASTEXITCODE"
        }
    }
}

# Restore in parallel first to speed things up
Function Restore-XProjectsFast {
    [CmdletBinding()]
    param(
        [string]$XProjectsLocation
    )
    $opts = 'restore', $XProjectsLocation, '--parallel', '--ignore-failed-sources'
    $opts += $PackageSources | %{ '-s', $_ }
    if (-not $VerbosePreference) {
        $opts += '--quiet'
    }

    Trace-Log "Restoring packages @""$XProjectsLocation"""
    Verbose-Log "dnu $opts"
    & dnu $opts 2>&1
    if (-not $?) {
        Error-Log "Restore failed @""$XProjectsLocation"". Code: $LASTEXITCODE"
    }
}

Function Find-XProjects($XProjectsLocation) {
    Get-ChildItem $XProjectsLocation -Recurse -Filter '*.xproj' |`
        %{ Split-Path $_.FullName -Parent }
}

Function Restore-XProjects {
    [CmdletBinding()]
    param(
        [string]$XProjectsLocation,
        [switch]$Fast
    )
    if ($Fast) {
        Restore-XProjectsFast $XProjectsLocation
    }
    else {
        $xprojects = Find-XProjects $XProjectsLocation
        $xprojects | Restore-XProject
    }
}

Function Invoke-DnuPack {
    [CmdletBinding()]
    param(
        [string]$XProjectsLocation,
        [string]$Configuration,
        [string]$ReleaseLabel,
        [int]$BuildNumber,
        [string]$Output
    )
    $BuildNumber = Format-BuildNumber $BuildNumber

    ## Setting the DNX build version
    if($ReleaseLabel -ne 'Release') {
        $env:DNX_BUILD_VERSION="${ReleaseLabel}-${BuildNumber}"
    }

    # Setting the DNX AssemblyFileVersion
    $env:DNX_ASSEMBLY_FILE_VERSION=$BuildNumber

    $xprojects = Find-XProjects $XProjectsLocation

    $opts = , 'pack'
    $opts += $xprojects
    $opts += '--configuration', $Configuration
    if ($Output) {
        $opts += '--out', $Output
    }
    if (-not $VerbosePreference) {
        $opts += '--quiet'
    }

    Verbose-Log "dnu $opts"
    &dnu $opts 2>&1
    if (-not $?) {
        Error-Log "Pack failed @""$XProjectsLocation"". Code: $LASTEXITCODE"
    }
}

Function Build-CoreProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$ReleaseLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [switch]$SkipRestore,
        [switch]$Fast
    )
    $XProjectsLocation = Join-Path $NuGetClientRoot src\NuGet.Core

    if (-not $SkipRestore) {
        Restore-XProjects $XProjectsLocation -Fast:$Fast
    }

    Invoke-DnuPack $XProjectsLocation $Configuration $ReleaseLabel $BuildNumber $Artifacts

    ## Moving nupkgs
    Trace-Log "Moving the packages to $Nupkgs"
    Get-ChildItem "${Artifacts}\*.nupkg" -Recurse | % { Move-Item $_ $Nupkgs }
}

Function Test-XProject {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True)]
        [string]$XProjectLocation
    )
    Process {
        Trace-Log "Running tests in ""$XProjectLocation"""

        $opts = '-p', $XProjectLocation, 'test'
        if ($VerbosePreference) {
            $opts += '-diagnostics', '-verbose'
        }
        else {
            $opts += '-nologo', '-quiet'
        }
        Verbose-Log "dnx $opts"

        # Check if dnxcore50 exists in the project.json file
        $xtestProjectJson = Join-Path $XProjectLocation "project.json"
        if (Get-Content $($xtestProjectJson) | Select-String "dnxcore50") {
            # Run tests for Core CLR
            Use-DNX CoreCLR
            & dnx $opts 2>&1
            if (-not $?) {
                Error-Log "Tests failed @""$XProjectLocation"" on CoreCLR. Code: $LASTEXITCODE"
            }
        }

        # Run tests for CLR
        Use-DNX CLR
        & dnx $opts 2>&1
        if (-not $?) {
            Error-Log "Tests failed @""$XProjectLocation"" on CLR. Code: $LASTEXITCODE"
        }
    }
}

Function Test-CoreProjects {
    [CmdletBinding()]
    param(
        [switch]$SkipRestore,
        [switch]$Fast
    )
    # Test assemblies should not be signed
    if (Test-Path Env:\DNX_BUILD_KEY_FILE) {
        Remove-Item Env:\DNX_BUILD_KEY_FILE
    }

    if (Test-Path Env:\DNX_BUILD_DELAY_SIGN) {
        Remove-Item Env:\DNX_BUILD_DELAY_SIGN
    }

    $XProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Core.Tests

    if (-not $SkipRestore) {
        Restore-XProjects $XProjectsLocation -Fast:$Fast
    }

    $xtests = Find-XProjects $XProjectsLocation
    $xtests | Test-XProject
}

Function Build-ClientsProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$ReleaseLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [switch]$SkipRestore,
        [switch]$Fast
    )
    #Building the microsoft interop package for the test.utility
    $interopLib = Join-Path $NuGetClientRoot lib\Microsoft.VisualStudio.ProjectSystem.Interop
    if (-not $SkipRestore) {
        Restore-XProject $interopLib -V2
    }
    Invoke-DnuPack $interopLib $Configuration $ReleaseLabel $BuildNumber
    Get-ChildItem "$interopLib\*.nupkg" -Recurse | % { Move-Item $_ $Nupkgs -Force }

    $solutionPath = Join-Path $NuGetClientRoot NuGet.Clients.sln
    if (-not $SkipRestore) {
        # Restore packages for NuGet.Tooling solution
        Restore-SolutionPackages -path $solutionPath -MSBuildVersion 14
    }

    # Build the solution
    $opts = , $solutionPath
    $opts += "/p:Configuration=$Configuration;ReleaseLabel=$ReleaseLabel;BuildNumber=$(Format-BuildNumber $BuildNumber)"
    if (-not $VerbosePreference) {
        $opts += '/verbosity:minimal'
    }

    Verbose-Log "$MSBuildExe $opts"
    & $MSBuildExe $opts
    if (-not $?) {
        Error-Log "Build of NuGet.Clients.sln failed. Code: $LASTEXITCODE"
    }

    Trace-Log "Copying the Vsix to $Artifacts"
    $visxLocation = Join-Path $Artifacts "$Configuration\NuGet.Clients\VsExtension"
    Copy-Item "$visxLocation\NuGet.Tools.vsix" $Artifacts
}

Function Test-ClientsProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration
    )
    $testProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Clients.Tests
    $testProjects = Get-ChildItem $testProjectsLocation -Recurse -Filter '*.csproj'`
        | %{ $_.FullName }`
        | ?{ -not $_.EndsWith('WebAppTest.csproj') }

    foreach($testProj in $testProjects) {
        $opts = $testProj, "/t:RunTests", "/p:Configuration=$Configuration;RunTests=true"
        if (-not $VerbosePreference) {
            $opts += '/verbosity:minimal'
        }
        Verbose-Log "$MSBuildExe $opts"
        & $MSBuildExe $opts
        if (-not $?) {
            Error-Log "Tests failed @""$testProj"". Code: $LASTEXITCODE"
        }
    }
}

# Merges the NuGet.exe
Function Invoke-ILMerge {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration
    )
    $nugetArtifactsFolder = Join-Path $Artifacts "$Configuration\NuGet.Clients\NuGet.CommandLine"
    $dlls = Get-ChildItem $nugetArtifactsFolder -Filter 'nuget.*.dll' | %{ $_.Name }
    $dlls += 'Microsoft.Web.XmlTransform.dll', 'Newtonsoft.Json.dll'

    Trace-Log 'Creating the ilmerged nuget.exe'
    $opts = , 'NuGet.exe'
    $opts += $dlls
    $opts += "/out:$Artifacts\NuGet.exe"
    if ($VerbosePreference) {
        $opts += '/log'
    }
    Verbose-Log "$ILMerge $opts"

    pushd $nugetArtifactsFolder
    try {
        & $ILMerge $opts 2>&1
    }
    finally {
        popd
    }
}