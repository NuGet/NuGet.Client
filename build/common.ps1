### Constants ###
$DefaultConfiguration = 'debug'
$DefaultReleaseLabel = 'zlocal'

$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent

# allow this to work for scripts/funcTests
if ((Split-Path -Path $PSScriptRoot -Leaf) -eq "scripts") {
    $NuGetClientRoot = Split-Path -Path $NuGetClientRoot -Parent
}

$MSBuildExe = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\14.0\Bin\msbuild.exe'
$NuGetExe = Join-Path $NuGetClientRoot '.nuget\nuget.exe'
$ILMerge = Join-Path $NuGetClientRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'
$XunitConsole = Join-Path $NuGetClientRoot 'packages\xunit.runner.console.2.1.0\tools\xunit.console.x86.exe'
$CLIRoot = Join-Path $NuGetClientRoot 'cli'
$DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
$Nupkgs = Join-Path $NuGetClientRoot nupkgs
$Artifacts = Join-Path $NuGetClientRoot artifacts
$Intermediate = Join-Path $Artifacts obj
$NuGetCoreSln = Join-Path $NuGetClientRoot 'NuGet.Core.sln'
$NuGetClientSln = Join-Path $NuGetClientRoot 'NuGet.Client.sln'

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
    $opts += '--init'
    if (-not $VerbosePreference) {
        $opts += '--quiet'
    }
    Trace-Log 'Updating and initializing submodules'
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

Function Install-DotnetCLI {
    [CmdletBinding()]
    param()

    Trace-Log 'Downloading Dotnet CLI'

    New-Item -ItemType Directory -Force -Path $CLIRoot | Out-Null

    $env:DOTNET_HOME=$CLIRoot

    $installDotnet = Join-Path $CLIRoot "install.ps1"
    $env:DOTNET_INSTALL_DIR=$NuGetClientRoot

    New-Item -ItemType Directory -Force -Path $CLIRoot

    wget https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1 -OutFile cli/install.ps1

    & cli/install.ps1 -Channel beta -i $CLIRoot -Version 1.0.0-rc2-002345

    if (-not (Test-Path $DotNetExe)) {
        Error-Log "Unable to find dotnet.exe. The CLI install may have failed."
    }

    # Display build info
    & $DotNetExe --info
}

# Enables delay signed build
Function Enable-DelaySigning {
    [CmdletBinding()]
    param($MSPFXPath, $NuGetPFXPath)
    if (Test-Path $MSPFXPath) {
        Trace-Log "Setting NuGet.Core solution to delay sign using $MSPFXPath"
        $env:DNX_BUILD_KEY_FILE=$MSPFXPath
        $env:DNX_BUILD_DELAY_SIGN=$true

        Trace-Log "Using the Microsoft Key for NuGet Command line $MSPFXPath"
        $env:MS_PFX_PATH=$MSPFXPath
    }

    if (Test-Path $NuGetPFXPath) {
        Trace-Log "Setting NuGet.Clients solution to delay sign using $NuGetPFXPath"
        $env:NUGET_PFX_PATH= $NuGetPFXPath
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
    Trace-Log 'Removing .NUGET packages'

    if (Test-Path $env:userprofile\.nuget\packages) {
        rm -r $env:userprofile\.nuget\packages -Force
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
        Remove-Item $Artifacts\* -Recurse -Force
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
        $opts += '-verbosity', 'quiet'
    }

    Trace-Log "Restoring packages @""$NuGetClientRoot"""
    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
    if (-not $?) {
        Error-Log "Restore failed @""$NuGetClientRoot"". Code: ${LASTEXITCODE}"
    }
}

# Restore nuget.core.sln projects
Function Restore-XProjects {

    $opts = 'restore', "src\NuGet.Core", "test\NuGet.Core.Tests", "test\NuGet.Core.FuncTests", "--verbosity", "minimal", "--infer-runtimes"

    Trace-Log "Restoring packages for xprojs"
    Verbose-Log "$dotnetExe $opts"
    & $dotnetExe $opts
    if (-not $?) {
        Error-Log "Restore failed @""$_"". Code: $LASTEXITCODE"
    }
}

Function Find-XProjects($XProjectsLocation) {
    Get-ChildItem $XProjectsLocation -Recurse -Filter '*.xproj' |`
        %{ Split-Path $_.FullName -Parent }
}

Function Invoke-DotnetPack {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$XProjectLocations,
        [Alias('config')]
        [string]$Configuration = $DefaultConfiguration,
        [Alias('label')]
        [string]$ReleaseLabel,
        [Alias('build')]
        [int]$BuildNumber,
        [Alias('out')]
        [string]$Output
    )
    Begin {
        $BuildNumber = Format-BuildNumber $BuildNumber

        # Setting the Dotnet AssemblyFileVersion
        $env:DOTNET_ASSEMBLY_FILE_VERSION=$BuildNumber
    }
    Process {
        $XProjectLocations | %{
            $opts = , 'pack'
            $opts += $_
            $opts += '--configuration', $Configuration

            if ($Output) {
                $opts += '--output', (Join-Path $Output (Split-Path $_ -Leaf))
            }

            if($ReleaseLabel -ne 'Release') {
                $opts += '--version-suffix', "${ReleaseLabel}-${BuildNumber}"
            }

            Trace-Log "$DotNetExe $opts"

            &$DotNetExe $opts
            if (-not $?) {
                Error-Log "Pack failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
    End { }
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

    # NuGet.Shared is a source package and fails when built as part of other projects.
    $sharedPath = Join-Path $XProjectsLocation "NuGet.Shared"
    Invoke-DotnetPack $sharedPath -config $Configuration -label $ReleaseLabel -build $BuildNumber -out $Artifacts

    $xprojects = Find-XProjects $XProjectsLocation
    $xprojects | Invoke-DotnetPack -config $Configuration -label $ReleaseLabel -build $BuildNumber -out $Artifacts

    ## Moving nupkgs
    Trace-Log "Moving the packages to $Nupkgs"
    Get-ChildItem "${Artifacts}\*.nupkg" -Recurse | % { Move-Item $_ $Nupkgs -Force }
}

Function Test-XProject {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$XProjectLocations,
        [string]$Configuration = $DefaultConfiguration
    )
    Process {
        $XProjectLocations | %{
            Trace-Log "Running tests in ""$_"""

            $directoryName = Split-Path $_ -Leaf

            pushd $_


                # Check if dnxcore50 exists in the project.json file
                $xtestProjectJson = Join-Path $_ "project.json"
                if (Get-Content $($xtestProjectJson) | Select-String "netcoreapp1.0") {
                    # Run tests for Core CLR

                    # Restore without runtimes
                    Trace-Log "$DotNetExe restore"
                    & $DotNetExe restore

                    # Build
                    Trace-Log "$DotNetExe build --configuration $Configuration --framework netcoreapp1.0"
                    & $DotNetExe build --configuration $Configuration --framework netcoreapp1.0

                    if (-not $?) {
                        Error-Log "Build failed for CoreCLR $directoryName. Code: $LASTEXITCODE"
                    }
                    else
                    {
                        Trace-Log "$DotNetExe test --configuration $Configuration --framework netcoreapp1.0 --no-build"
                        & $DotNetExe test --configuration $Configuration --framework netcoreapp1.0 --no-build
                        if (-not $?) {
                            Error-Log "Tests failed @""$_"" on CoreCLR. Code: $LASTEXITCODE"
                        }
                    }
                }

                # Run tests for CLR
                if (Get-Content $($xtestProjectJson) | Select-String "net46") {
                    # Restore with runtimes
                    Trace-Log "$DotNetExe restore --infer-runtimes"
                    & $DotNetExe restore --infer-runtimes

                    # Build
                    Trace-Log "$DotNetExe build --configuration $Configuration --runtime win7-x64 --framework net46"
                    & $DotNetExe build --configuration $Configuration --runtime win7-x64 --framework net46

                    if (-not $?) {
                        Error-Log "Build failed for net46 $directoryName. Code: $LASTEXITCODE"
                    }
                    else
                    {
                        $htmlOutput = Join-Path $_ "bin\$Configuration\net46\win7-x64\xunit.results.html"
                        $desktopTestAssembly = Join-Path $_ "bin\$Configuration\net46\win7-x64\$directoryName.dll"

                        Trace-Log "$XunitConsole $desktopTestAssembly -html $htmlOutput"

                        & $XunitConsole $desktopTestAssembly -html $htmlOutput
                        if (-not $?) {
                           Error-Log "Tests failed @""$_"" on CLR. Code: $LASTEXITCODE"
                        }
                    }
                }

            popd
        }
    }
    End {}
}

Function Test-CoreProjects {
    [CmdletBinding()]
    param(
        [switch]$SkipRestore,
        [switch]$Fast,
        [string]$Configuration = $DefaultConfiguration
    )
    $XProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Core.Tests

    $xtests = Find-XProjects $XProjectsLocation
    $xtests | Test-XProject -Configuration $Configuration
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

    Trace-Log "$MSBuildExe $opts"
    & $MSBuildExe $opts
    if (-not $?) {
        Error-Log "Build of NuGet.Clients.sln failed. Code: $LASTEXITCODE"
    }
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
        Test-ClientProject $testProj -Configuration $Configuration
    }
}

Function Test-ClientProject {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string]$testProj,
        [string]$Configuration = $DefaultConfiguration
    )
    $opts = $testProj, "/t:RunTests", "/p:Configuration=$Configuration;RunTests=true"
    if (-not $VerbosePreference) {
        $opts += '/verbosity:minimal'
    }
    Trace-Log "$MSBuildExe $opts"
    & $MSBuildExe $opts
    if (-not $?) {
        Error-Log "Tests failed @""$testProj"". Code: $LASTEXITCODE"
    }
}

Function Read-FileList($FilePath) {
    Get-Content $FilePath | ?{ -not $_.StartsWith('#') } | %{ $_.Trim() } | ?{ $_ -ne '' }
}

# Merges the NuGet.exe
Function Invoke-ILMerge {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration
    )
    $buildArtifactsFolder = [io.path]::combine($Artifacts, 'NuGet.CommandLine', $Configuration)
    $ignoreList = Read-FileList (Join-Path $buildArtifactsFolder '.mergeignore')
    $buildArtifacts = Get-ChildItem $buildArtifactsFolder -Exclude $ignoreList | %{ $_.Name }

    $includeList = Read-FileList (Join-Path $buildArtifactsFolder '.mergeinclude')
    $notInList = $buildArtifacts | ?{ -not ($includeList -contains $_) }
    if ($notInList) {
        Error-Log "Found build artifacts NOT listed in include list: $($notInList -join ', ')"
    }
    $notFound = $includeList | ?{ -not ($buildArtifacts -contains $_) }
    if ($notFound) {
        Error-Log "Missing build artifacts listed in include list: $($notFound -join ', ')"
    }

	# Note that the Dev14 path will become conditional on which target we're building against (TODO)
    Trace-Log 'Creating the ilmerged nuget.exe'
    $opts = , 'NuGet.exe'
    $opts += $buildArtifacts
    $opts += "/out:$Artifacts\NuGet.exe"
    if ($VerbosePreference) {
        $opts += '/log'
    }
    Verbose-Log "$ILMerge $opts"

    pushd $buildArtifactsFolder
    try {
        & $ILMerge $opts 2>&1
    }
    finally {
        popd
    }
}