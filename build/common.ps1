### Constants ###
$ValidConfigurations = 'debug', 'release'
$DefaultConfiguration = 'debug'
$ValidReleaseLabels = 'Release','rtm', 'rc', 'beta', 'local'
$DefaultReleaseLabel = 'local'

$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$MSBuildExe = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\msbuild.exe'
$NuGetExe = Join-Path $NuGetClientRoot '.nuget\nuget.exe'
$ILMerge = Join-Path $NuGetClientRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'
$DnvmCmd = Join-Path $env:USERPROFILE '.dnx\bin\dnvm.cmd'
$Nupkgs = Join-Path $NuGetClientRoot nupkgs
$Artifacts = Join-Path $NuGetClientRoot artifacts

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
        try {
            Invoke-Command $Expression -ArgumentList $Arguments -ErrorVariable err
        }
        finally {
            $sw.Stop()
            Reset-Colors
            if (-not $err) {
                Trace-Log "[DONE +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
            }
            else {
                Error-Log "[FAILED +$(Format-ElapsedTime $sw.Elapsed)] $BuildStep"
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
    if (-not (Test-Path -Path "$NuGetClientRoot/submodules/FileSystem/src")) {
        Trace-Log 'Updating and initializing submodules'
        & git submodule update --init 2>&1
    }
    else {
        Trace-Log 'Updating submodules'
        & git submodule update 2>&1
    }
}

# Downloads NuGet.exe if missing
Function Install-NuGet() {
    if (-not (Test-Path $NuGetExe)) {
        Trace-Log 'Downloading nuget.exe'
        wget https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe -OutFile $NuGetExe
    }
}

# Validates DNVM installed and installs it if missing
Function Install-DNVM() {
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
Function Install-DNX() {
    Trace-Log 'Validating the correct DNX runtime set'
    $env:DNX_FEED = 'https://www.nuget.org/api/v2'
    & dnvm install 1.0.0-rc1-update1 -runtime CoreCLR -arch x86 2>&1
    & dnvm install 1.0.0-rc1-update1 -runtime CLR -arch x86 -alias default 2>&1
}

Function Set-DNXCLR() {
    Trace-Log 'Setting DNX to CLR x86'
    & dnvm use 1.0.0-rc1-update1 -runtime CLR -arch x86 2>&1
}

Function Set-DNXCoreCLR() {
    Trace-Log 'Setting DNX to CoreCLR x86'
    & dnvm use 1.0.0-rc1-update1 -runtime CoreCLR -arch x86 2>&1
}

# Enables delay signed build
Function Enable-DelayedSigning($MSPFXPath, $NuGetPFXPath) {
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
Function Clear-PackageCache() {
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

Function Clear-Artifacts() {
    if( Test-Path $Artifacts) {
        Trace-Log 'Cleaning the Artifacts folder'
        Remove-Item $Artifacts\*.* -Recurse -Force
    }
}

Function Clear-Nupkgs() {
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
    Trace-Log "$NuGetExe $opts"
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
        [string[]]$XProjectLocation,
        [switch]$V2
    )
    Process {
        $projectJsonFile = Join-Path $XProjectLocation 'project.json'
        $opts = 'restore', $projectJsonFile
        if (-not $V2) {
            $opts += '-s', 'https://www.myget.org/F/nuget-volatile/api/v3/index.json'
            $opts += '-s', 'https://api.nuget.org/v3/index.json'
        }
        else {
            $opts += '-s', 'https://www.myget.org/F/nuget-volatile/api/v2/'
            $opts += '-s', 'https://www.nuget.org/api/v2/'
        }
        if (-not $VerbosePreference) {
            $opts += '--quiet'
        }

        Trace-Log "Restoring packages @""$XProjectLocation"""
        Trace-Log "dnu $opts"
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
    $opts += '-s', 'https://www.myget.org/F/nuget-volatile/api/v3/index.json'
    $opts += '-s', 'https://api.nuget.org/v3/index.json'
    if (-not $VerbosePreference) {
        $opts += '--quiet'
    }

    Trace-Log "Restoring packages @""$XProjectsLocation"""
    Trace-Log "dnu $opts"
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
    param(
        [string]$XProjectsLocation,
        [switch]$Fast
    )
    if ($Fast) {
        Restore-XProjectsFast $XProjectsLocation
    }
    else {
        $xprojects = Find-XProjects $XProjectsLocation
        $xprojects | Restore-XProject -Verbose
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

    Trace-Log "dnu $opts"
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

    Invoke-DnuPack $XProjectsLocation $Configuration $ReleaseLabel $BuildNumber $Artifacts -Verbose:(-not $Fast)

    ## Moving nupkgs
    Trace-Log "Moving the packages to $Nupkgs"
    Get-ChildItem "${Artifacts}\*.nupkg" -Recurse | % { Move-Item $_ $Nupkgs }
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
    foreach ($xtestLocation in $xtests) {
        Trace-Log "Running tests in ""$xtestLocation"""

        pushd $xtestLocation

        # Run tests for Core CLR

        $xtestProjectJson = Join-Path $xtestLocation "project.json"

        # Check if dnxcore50 exists in the project.json file
        if (Get-Content $($xtestProjectJson) | Select-String "dnxcore50") {
            Set-DNXCoreCLR
            & dnx test 2>&1

            if (-not $?) {
                Error-Log "Tests failed @""$xtestLocation"" on CoreCLR. Code: $LASTEXITCODE"
            }
        }

        # Run tests for CLR
        Set-DNXCLR
        & dnx test 2>&1

        if (-not $?) {
            Error-Log "Tests failed @""$xtestLocation"" on CLR. Code: $LASTEXITCODE"
        }

        popd
    }
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
        Restore-XProject $interopLib -V2 -Verbose:(-not $Fast)
    }
    Invoke-DnuPack $interopLib $Configuration $ReleaseLabel $BuildNumber -Verbose:(-not $Fast)
    Get-ChildItem "$interopLib\*.nupkg" -Recurse | % { Move-Item $_ $Nupkgs -Force }

    $solutionPath = Join-Path $NuGetClientRoot NuGet.Clients.sln
    if (-not $SkipRestore) {
        # Restore packages for NuGet.Tooling solution
        Restore-SolutionPackages -path $solutionPath -MSBuildVersion 14 -Verbose:(-not $Fast)
    }

    # Build the solution
    $opts = , $solutionPath
    $opts += "/p:Configuration=$Configuration;ReleaseLabel=$ReleaseLabel;BuildNumber=$(Format-BuildNumber $BuildNumber)"
    if ($Fast) {
        $opts += '/verbosity:minimal'
    }

    Trace-Log "$MSBuildExe $opts"
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
        Trace-Log "$MSBuildExe $opts"
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
    pushd $nugetArtifactsFolder

    Trace-Log 'Creating the ilmerged nuget.exe'
    & $ILMerge NuGet.exe NuGet.Client.dll NuGet.Commands.dll NuGet.Configuration.dll NuGet.ContentModel.dll NuGet.Core.dll NuGet.Credentials.dll NuGet.DependencyResolver.Core.dll NuGet.DependencyResolver.dll NuGet.Frameworks.dll NuGet.LibraryModel.dll NuGet.Logging.dll NuGet.PackageManagement.dll NuGet.Packaging.Core.dll NuGet.Packaging.Core.Types.dll NuGet.Packaging.dll NuGet.ProjectManagement.dll NuGet.ProjectModel.dll NuGet.Protocol.Core.Types.dll NuGet.Protocol.Core.v2.dll NuGet.Protocol.Core.v3.dll NuGet.Repositories.dll NuGet.Resolver.dll NuGet.RuntimeModel.dll NuGet.Versioning.dll Microsoft.Web.XmlTransform.dll Newtonsoft.Json.dll /log:mergelog.txt /out:$Artifacts\NuGet.exe 2>&1

    popd
}