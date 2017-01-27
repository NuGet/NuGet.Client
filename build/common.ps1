### Constants ###
$DefaultConfiguration = 'debug'
$DefaultReleaseLabel = 'zlocal'
$DefaultMSBuildVersion = 15

# The pack version can be inferred from the .nuspec files on disk. This is only necessary as long
# as the following issue is open: https://github.com/NuGet/Home/issues/3530
$PackageReleaseVersion = "4.0.0"

$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent
$CLIRoot = Join-Path $NuGetClientRoot cli
$CLIRootTest = Join-Path $NuGetClientRoot cli_test
$Nupkgs = Join-Path $NuGetClientRoot nupkgs
$Artifacts = Join-Path $NuGetClientRoot artifacts
$ReleaseNupkgs = Join-Path $Artifacts ReleaseNupkgs
$ConfigureJson = Join-Path $Artifacts configure.json
$ILMergeOutputDir = Join-Path $Artifacts "VS14"

$DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
$DotNetExeTest = Join-Path $CLIRootTest 'dotnet.exe'
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
    '{0:F0}:{1:D2}' -f $ElapsedTime.TotalMinutes, $ElapsedTime.Seconds
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
        wget https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe -OutFile $NuGetExe
    }

    # Display nuget info
    & $NuGetExe locals all -list -verbosity detailed
}

Function Install-DotnetCLI {
    [CmdletBinding()]
    param(
        [switch]$Test,
        [switch]$Force
    )

    $cli = if (-not $Test) {
        @{
            Root = $CLIRoot
            DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
            DotNetInstallUrl = 'https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview2/scripts/obtain/dotnet-install.ps1'
            Version = '1.0.0-preview2-003131'
        }
    }
    else {
        @{
            Root = $CLIRootTest
            DotNetExe = Join-Path $CLIRootTest 'dotnet.exe'
            DotNetInstallUrl = 'https://raw.githubusercontent.com/dotnet/cli/58b0566d9ac399f5fa973315c6827a040b7aae1f/scripts/obtain/dotnet-install.ps1'
            Version = '1.0.0-rc4-004616'
        }
    }

    $env:DOTNET_HOME=$cli.Root
    $env:DOTNET_INSTALL_DIR=$NuGetClientRoot

    if ($Force -or -not (Test-Path $cli.DotNetExe)) {
        Trace-Log 'Downloading .NET CLI'

        New-Item -ItemType Directory -Force -Path $cli.Root | Out-Null

        $DotNetInstall = Join-Path $cli.Root 'dotnet-install.ps1'

        Invoke-WebRequest $cli.DotNetInstallUrl -OutFile $DotNetInstall

        & $DotNetInstall -Channel preview -i $cli.Root -Version $cli.Version
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
        [ValidateSet(14,15)]
        [int]$MSBuildVersion
    )
    # Get the highest msbuild version if version was not specified
    if (-not $MSBuildVersion) {
        $MSBuildExe = Get-MSBuildExe 15
        if (Test-Path $MSBuildExe) {
            return $MSBuildExe
        }

        return Get-MSBuildExe 14
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
        Error-Log 'Build environment is not configured. Please run configure.ps1 first.' -Fatal
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

function Enable-DelaySigningForDotNet {
    param(
        $xproject,
        $KeyFile
    )
    Verbose-Log "Adding keyFile '$KeyFile' to buildOptions"

    $buildOptions = $xproject.buildOptions

    if ($buildOptions -eq $null) {
        $newSection = ConvertFrom-Json -InputObject '{ }'
        $xproject | Add-Member -Name "buildOptions" -value $newSection -MemberType NoteProperty
        $buildOptions = $xproject.buildOptions
    }

    if (-not $xproject.buildOptions.keyFile) {
        $buildOptions | Add-Member -Name "keyFile" -value $KeyFile -MemberType NoteProperty
    }
    else {
        Warning-Log "keyFile already exists"
    }

    if (-not $xproject.buildOptions.delaySign) {
        $buildOptions | Add-Member -Name "delaySign" -value $true -MemberType NoteProperty
    }
    else {
        Warning-Log "delaySign already exists"
    }
}

Function Save-ProjectFile ($xproject, $fileName) {
    Trace-Log "Saving project to '$fileName'"
    $xproject | ConvertTo-Json -Depth 100 | Out-File $fileName
}

Function Set-DelaySigning {
    [CmdletBinding()]
    param(
        [string]$MSPFXPath,
        [string]$NuGetPFXPath
    )

    if ($MSPFXPath -and (Test-Path $MSPFXPath)) {
        Trace-Log "Setting NuGet.Core solution to delay sign using $MSPFXPath"

        Trace-Log "Using the Microsoft Key for NuGet Command line $MSPFXPath"
        $env:MS_PFX_PATH=$MSPFXPath

        $XProjectsLocation = Join-Path $NuGetClientRoot '\src\NuGet.Core'
        Trace-Log "Adding KeyFile '$MSPFXPath' to project files in '$XProjectsLocation'"
        (Get-ChildItem $XProjectsLocation -rec -Filter 'project.json') |
            %{ $_.FullName } |
            %{
                Verbose-Log "Processing '$_'"
                $xproject = (Get-Content $_ -Raw) | ConvertFrom-Json
                if (-not $xproject) {
                    Write-Error "'$_' is not a valid json file"
                }
                else {
                    Enable-DelaySigningForDotNet $xproject $MSPFXPath
                    Save-ProjectFile $xproject $_
                }
            }
    }
    else {
        Remove-Item Env:\MS_PFX_PATH -ErrorAction Ignore
    }

    if ($NuGetPFXPath -and (Test-Path $NuGetPFXPath)) {
        Trace-Log "Setting NuGet.Clients solution to delay sign using $NuGetPFXPath"
        $env:NUGET_PFX_PATH= $NuGetPFXPath
    }
    else {
        Remove-Item Env:\NUGET_PFX_PATH -ErrorAction Ignore
    }
}

Function Get-BuildNumber() {
    $SemanticVersionDate = '2016-07-13'
    [int](((Get-Date) - (Get-Date $SemanticVersionDate)).TotalMinutes / 5)
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
        [ValidateSet(4, 12, 14, 15)]
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
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$XProjectLocations
    )
    end {
        $xprojects = $Input | Join-Path -ChildPath project.json -Resolve
        $xprojects | %{
            $opts = 'restore', $_
            if (-not $VerbosePreference) {
                $opts += '--verbosity', 'minimal'
            } else {
                $opts += '--verbosity', 'information'
            }

            Trace-Log "$DotNetExe $opts"
            & $DotNetExe $opts
            if (-not $?) {
                Error-Log "Restore failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
}

Function Find-XProjects([string]$XProjectsLocation) {
    Get-ChildItem $XProjectsLocation -Recurse -Filter '*.xproj' |
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
        [string]$NupkgOutput
    )
    Begin {
        $BuildNumber = Format-BuildNumber $BuildNumber

        # Setting the Dotnet AssemblyFileVersion
        $env:DOTNET_ASSEMBLY_FILE_VERSION=$BuildNumber
    }
    Process {
        $XProjectLocations | % {
            $projectName = Split-Path $_ -Leaf

            $opts = @()

            if ($VerbosePreference) {
                $opts += '-v'
            }

            $opts += 'pack', $_, '--configuration', $Configuration

            $versionSuffix = ''

            if ($ReleaseLabel -And $BuildNumber) {
                $versionSuffix = "$ReleaseLabel-$BuildNumber"
            } elseif ($ReleaseLabel -Ne 'rtm') {
                $versionSuffix = $ReleaseLabel
            }

            if ($versionSuffix) {
                $opts += '--version-suffix', $versionSuffix
            }

            if ($NupkgOutput) {
                $opts += '--output', $NupkgOutput
            }

            $opts += '--build-base-path', $Artifacts
            $opts += '--serviceable'

            Trace-Log "$DotNetExe $opts"

            & $DotNetExe $opts
            if (-not $?) {
                Error-Log "Pack failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
}

Function Invoke-DotnetBuild {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$XProjectLocations,
        [Alias('config')]
        [string]$Configuration = $DefaultConfiguration
    )
    Begin {
        $BuildNumber = Format-BuildNumber $BuildNumber

        # Setting the Dotnet AssemblyFileVersion
        $env:DOTNET_ASSEMBLY_FILE_VERSION=$BuildNumber
    }
    Process {
        $XProjectLocations | % {
            $projectName = Split-Path $_ -Leaf

            $opts = @()

            if ($VerbosePreference) {
                $opts += '-v'
            }

            $opts += 'build', $_, '--configuration', $Configuration

            Trace-Log "$DotNetExe $opts"

            & $DotNetExe $opts
            if (-not $?) {
                Error-Log "Build failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
}

Function Publish-CoreProject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$True)]
        [string]$XProjectLocation,
        [Parameter(Mandatory=$True)]
        [string]$PublishLocation,
        [string]$Configuration = $DefaultConfiguration
    )
    $opts = @()

    if ($VerbosePreference) {
        $opts += '-v'
    }

    $opts += 'publish', $XProjectLocation
    $opts += '--configuration', $Configuration, '--framework', 'netcoreapp1.0'
    $opts += '--no-build'

    $opts += '--build-base-path', $Artifacts

    $OutputDir = Join-Path $PublishLocation "$Configuration\netcoreapp1.0"
    $opts += '--output', $OutputDir

    Trace-Log "$DotNetExe $opts"
    & $DotNetExe $opts

    if (-not $?) {
        Error-Log "Publish project failed @""$XProjectLocation"". Code: $LASTEXITCODE"
    }
}

Function Build-CoreProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$ReleaseLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [switch]$SkipRestore,
        [switch]$CI
    )
    $XProjectsLocation = Join-Path $NuGetClientRoot src\NuGet.Core -Resolve
    $xprojects = Find-XProjects $XProjectsLocation

    if (-not $SkipRestore) {
        $xprojects | Restore-XProjects
    }

    # Build .nupkgs for MyGet (which have release label and build number)
    $xprojects | Invoke-DotnetPack -config $Configuration -label $ReleaseLabel -build $BuildNumber -out $Nupkgs

    if ($CI) {
        # Build .nupkgs for release (which have no build number). This will re-use the build from the last
        # step because the --build-base-path is the same.
        $xprojects | Invoke-DotnetPack -config $Configuration -label $ReleaseLabel -out $ReleaseNupkgs
    }

    # Publish NuGet.CommandLine.XPlat
    $PublishLocation = Join-Path $Artifacts "NuGet.CommandLine.XPlat\publish"
    Trace-Log "Publishing XPlat project to '$PublishLocation'"
    $XPlatProject = Join-Path $NuGetClientRoot 'src\NuGet.Core\NuGet.CommandLine.XPlat'
    Publish-CoreProject $XPlatProject $PublishLocation $Configuration

    #Pack NuGet.Build.Tasks.Pack using the nuspec file and copy to the artifacts and artifacts\ReleaseNupkgs folder
    Pack-NuGetBuildTasksPack -config $Configuration -label $ReleaseLabel -buildNum $BuildNumber -CI:$CI
}

Function Pack-NuGetBuildTasksPack {
    [CmdletBinding()]
    param(
        [Alias('config')]
        [string]$Configuration,
        [Alias('label')]
        [string]$ReleaseLabel,
        [Alias('buildNum')]
        [string]$BuildNumber,
        [switch]$CI
    )

    $prereleaseNupkgVersion = "$PackageReleaseVersion-$ReleaseLabel-$BuildNumber"
    if ($ReleaseLabel -Ne 'rtm') {
        $releaseNupkgVersion = "$PackageReleaseVersion-$ReleaseLabel"
    } else {
        $releaseNupkgVersion = "$PackageReleaseVersion"
    }

    $PackProjectLocation = Join-Path $NuGetClientRoot src\NuGet.Core\NuGet.Build.Tasks.Pack.Library
    $PackBuildTaskNuspecLocation = Join-Path $PackProjectLocation NuGet.Build.Tasks.Pack.nuspec

    New-NuGetPackage `
        -NuspecPath $PackBuildTaskNuspecLocation `
        -BasePath $PackProjectLocation `
        -OutputDir $Nupkgs `
        -Version $prereleaseNupkgVersion `
        -Configuration $Configuration

    if ($CI) {
        New-NuGetPackage `
            -NuspecPath $PackBuildTaskNuspecLocation `
            -BasePath $PackProjectLocation `
            -OutputDir $ReleaseNupkgs `
            -Version $releaseNupkgVersion `
            -Configuration $Configuration
    }
}

Function Test-XProjectCoreClr {
    [CmdletBinding()]
    param(
        [string]$XProjectLocation,
        [string]$Configuration = $DefaultConfiguration
    )
    $opts = @()

    if ($VerbosePreference) {
        $opts += '-v'
    }

    $opts += 'test', '--configuration', $Configuration, '--framework', 'netcoreapp1.0'
    $opts += '-notrait', 'Platform=Linux', '-notrait', 'Platform=Darwin'

    if ($VerbosePreference) {
        $opts += '-verbose'
    }

    pushd $XProjectLocation

    try {
        Trace-Log "$DotNetExe $opts"
        & $DotNetExe $opts
    }
    finally {
        popd
    }

    if ($LASTEXITCODE -ne 0) {
        Error-Log "Tests failed @""$XProjectLocation"" on CoreCLR. Code: $LASTEXITCODE"
    }
}

Function Test-XProjectClr {
    [CmdletBinding()]
    param(
        [string]$XProjectLocation,
        [string]$Configuration = $DefaultConfiguration
    )
    # Build
    $opts = @()

    if ($VerbosePreference) {
        $opts += '-v'
    }

    $opts += 'build', '--configuration', $Configuration, '--runtime', 'win7-x64'

    pushd $XProjectLocation

    try {
        Trace-Log "$DotNetExe $opts"
        & $DotNetExe $opts
    }
    finally {
        popd
    }

    if ($LASTEXITCODE -ne 0) {
        Error-Log "Build failed @""$_"" on CLR. Code: $LASTEXITCODE"
    }
    else {
        $directoryName = Split-Path $_ -Leaf
        $htmlOutput = Join-Path $XProjectLocation "bin\$Configuration\net46\win7-x64\xunit.results.html"
        $desktopTestAssembly = Join-Path $XProjectLocation "bin\${Configuration}\net46\win7-x64\${directoryName}.dll"
        $opts = $desktopTestAssembly, '-html', $htmlOutput
        $opts += '-notrait', 'Platform=Linux', '-notrait', 'Platform=Darwin'

        if ($VerbosePreference) {
            $opts += '-verbose'
        }

        Trace-Log "$XunitConsole $opts"
        & $XunitConsole $opts
        if (-not $?) {
            Error-Log "Tests failed @""$XProjectLocation"" on CLR. Code: $LASTEXITCODE"
        }
    }
}

Function Test-XProject {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$XProjectLocations,
        [string]$Configuration = $DefaultConfiguration
    )
    Process {
        $XProjectLocations | Resolve-Path | %{
            $xtestProjectJson = Join-Path $_ project.json -Resolve
            $xproject = gc $xtestProjectJson -raw | ConvertFrom-Json

            if ($xproject.testRunner) {
                Trace-Log "Running tests in ""$_"""

                # Check if netcoreapp1.0 exists in the project.json file
                if ($xproject.frameworks.'netcoreapp1.0') {
                    # Run tests for Core CLR
                    Test-XProjectCoreClr $_ $Configuration
                }

                # Run tests for CLR
                if ($xproject.frameworks.net46) {
                    Test-XProjectClr $_ $Configuration
                }
            }
            else {
                Trace-Log "Skipping non-test project in ""$_"""
            }
        }
    }
}

Function Test-CoreProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration
    )
    $XProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Core.Tests
    Test-CoreProjectsHelper $Configuration $XProjectsLocation
}

Function Test-FuncCoreProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration
    )
    $XProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Core.FuncTests
    Test-CoreProjectsHelper $Configuration $XProjectsLocation
}

Function Test-CoreProjectsHelper {
    [CmdletBinding()]
    param(
        [string]$Configuration,
        [string]$XProjectsLocation
    )

    # Restore both src and test core projects.
    $srcLocation = Join-Path $NuGetClientRoot src\NuGet.Core -Resolve
    $xprojs = Find-XProjects $srcLocation
    $xtests = Find-XProjects $XProjectsLocation
    $xprojs + $xtests | Restore-XProjects

    # Test all core test projects.
    $xtests | Test-XProject -Configuration $Configuration
}

Function Build-ClientsProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$ReleaseLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [ValidateSet(14,15)]
        [int]$ToolsetVersion = $DefaultMSBuildVersion,
        [switch]$SkipRestore
    )

    $solutionPath = Join-Path $NuGetClientRoot NuGet.Clients.sln -Resolve

    if (-not $SkipRestore) {
        # Override VisualStudioVersion for following solution restore operation.
        # Needed to lock correct VS15/VS14 packages in the facade project.
        $vsv = $env:VisualStudioVersion
        $env:VisualStudioVersion = "${ToolsetVersion}.0"

        # Restore packages for NuGet.Tooling solution using default msbuild
        try {
            Restore-SolutionPackages -path $solutionPath
        }
        finally {
            $env:VisualStudioVersion = $vsv
        }
    }

    # Build the solution
    Build-ClientsProjectHelper `
        -SolutionOrProject $solutionPath `
        -Configuration $Configuration `
        -ReleaseLabel $ReleaseLabel `
        -BuildNumber $BuildNumber `
        -ToolsetVersion $ToolsetVersion `
        -IsSolution
}

Function Publish-ClientsPackages {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$ReleaseLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [ValidateSet(14,15)]
        [int]$ToolsetVersion = $DefaultMSBuildVersion,
        [string]$KeyFile,
        [switch]$CI
    )

    $prereleaseNupkgVersion = "$PackageReleaseVersion-$ReleaseLabel-$BuildNumber"
    if ($ReleaseLabel -Ne 'rtm') {
        $releaseNupkgVersion = "$PackageReleaseVersion-$ReleaseLabel"
    } else {
        $releaseNupkgVersion = "$PackageReleaseVersion"
    }

    $exeProjectDir = [io.path]::combine($NuGetClientRoot, "src", "NuGet.Clients", "NuGet.CommandLine")
    $exeProject = Join-Path $exeProjectDir "NuGet.CommandLine.csproj"
    $exeNuspec = Join-Path $exeProjectDir "NuGet.CommandLine.nuspec"
    $exeInputDir = [io.path]::combine($Artifacts, "NuGet.CommandLine", "${ToolsetVersion}.0", $Configuration)
    $exeOutputDir = Join-Path $Artifacts "VS${ToolsetVersion}"

    # Build and pack the NuGet.CommandLine project with the build number and release label.
    Build-ClientsProjectHelper `
        -SolutionOrProject $exeProject `
        -Configuration $Configuration `
        -ReleaseLabel $ReleaseLabel `
        -BuildNumber $BuildNumber `
        -ToolsetVersion $ToolsetVersion `
        -Rebuild

    Invoke-ILMerge `
        -InputDir $exeInputDir `
        -OutputDir $exeOutputDir `
        -KeyFile $KeyFile

    New-NuGetPackage `
        -NuspecPath $exeNuspec `
        -BasePath $exeOutputDir `
        -OutputDir $Nupkgs `
        -Version $prereleaseNupkgVersion `
        -Configuration $Configuration

    # Build and pack the NuGet.CommandLine project with just the release label.
    Build-ClientsProjectHelper `
        -SolutionOrProject $exeProject `
        -Configuration $Configuration `
        -ReleaseLabel $ReleaseLabel `
        -BuildNumber $BuildNumber `
        -ToolsetVersion $ToolsetVersion `
        -ExcludeBuildNumber `
        -Rebuild

    Invoke-ILMerge `
        -InputDir $exeInputDir `
        -OutputDir $exeOutputDir `
        -KeyFile $KeyFile

    if ($CI) {
        New-NuGetPackage `
            -NuspecPath $exeNuspec `
            -BasePath $exeOutputDir `
            -OutputDir $ReleaseNupkgs `
            -Version $releaseNupkgVersion `
            -Configuration $Configuration
    }

    # Pack the NuGet.VisualStudio project with the build number and release label.
    $projectDir = [io.path]::combine($NuGetClientRoot, "src", "NuGet.Clients", "NuGet.VisualStudio")
    $projectNuspec = Join-Path $projectDir "NuGet.VisualStudio.nuspec"
    $projectInputDir = [io.path]::combine($Artifacts, "NuGet.VisualStudio", "${ToolsetVersion}.0", "${Configuration}")
    $projectInstallPs1 = Join-Path $projectDir "install.ps1"

    Copy-Item -Path "${projectInstallPs1}" -Destination "${projectInputDir}"

    New-NuGetPackage `
        -NuspecPath $projectNuspec `
        -BasePath $projectInputDir `
        -OutputDir $Nupkgs `
        -Version $prereleaseNupkgVersion `
        -Configuration $Configuration

    if ($CI) {
        # Pack the NuGet.VisualStudio project with just the release label.
        New-NuGetPackage `
            -NuspecPath $projectNuspec `
            -BasePath $projectInputDir `
            -OutputDir $ReleaseNupkgs `
            -Version $releaseNupkgVersion `
            -Configuration $Configuration
    }
}

Function New-NuGetPackage {
    [CmdletBinding()]
    param(
        [string]$NuspecPath,
        [string]$BasePath,
        [string]$OutputDir,
        [string]$Version,
        [string]$Configuration=$DefaultConfiguration
    )

    $opts = 'pack', $NuspecPath
    $opts += '-BasePath', $BasePath
    $opts += '-OutputDirectory', $OutputDir
    $opts += '-Symbols'
    $opts += '-Version', $Version
    $opts += '-Properties', "Configuration=$Configuration"

    if ($VerbosePreference) {
        $opts += '-verbosity', 'detailed'
    }
    else {
        $opts += '-verbosity', 'quiet'
    }

    Trace-Log "$NuGetExe $opts"
    & $NuGetExe $opts
}

Function Build-ClientsProjectHelper {
    param(
        [string]$SolutionOrProject,
        [string]$Configuration = $DefaultConfiguration,
        [string]$ReleaseLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [ValidateSet(14,15)]
        [int]$ToolsetVersion = $DefaultMSBuildVersion,
        [switch]$IsSolution,
        [switch]$ExcludeBuildNumber,
        [switch]$Rebuild
    )

    $opts = , $SolutionOrProject

    if ($Rebuild) {
        $opts += "/t:Rebuild"
    }

    if ($IsSolution -And $ToolsetVersion -eq 14) {
        $opts += "/p:Configuration=$Configuration VS14"
    }
    else {
        $opts += "/p:Configuration=$Configuration"
    }

    $opts += "/p:ReleaseLabel=$ReleaseLabel;BuildNumber=$(Format-BuildNumber $BuildNumber)"

    if ($ExcludeBuildNumber) {
        $opts += "/p:ExcludeBuildNumber=true"
    }

    $opts += "/p:VisualStudioVersion=${ToolsetVersion}.0"
    $opts += "/tv:${ToolsetVersion}.0"

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
        [string]$Configuration = $DefaultConfiguration,
        [ValidateSet(14,15)]
        [int]$ToolsetVersion = $DefaultMSBuildVersion,
        [string[]]$SkipProjects,
        [switch]$CI
    )

    $TestProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Clients.Tests -Resolve

    Test-ClientsProjectsHelper `
        -Configuration $Configuration `
        -ToolsetVersion $ToolsetVersion `
        -SkipProjects $SkipProjects `
        -TestProjectsLocation $TestProjectsLocation `
        -CI:$CI
}

Function Test-FuncClientsProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [ValidateSet(14,15)]
        [int]$ToolsetVersion = $DefaultMSBuildVersion,
        [string[]]$SkipProjects,
        [switch]$CI
    )

    $TestProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Clients.FuncTests -Resolve

    Test-ClientsProjectsHelper `
        -Configuration $Configuration `
        -ToolsetVersion $ToolsetVersion `
        -SkipProjects $SkipProjects `
        -TestProjectsLocation $TestProjectsLocation `
        -CI:$CI
}

Function Test-ClientsProjectsHelper {
    [CmdletBinding()]
    param(
        [string]$TestProjectsLocation,
        [string]$Configuration,
        [int]$ToolsetVersion,
        [string[]]$SkipProjects,
        [switch]$CI
    )

    if (-not $SkipProjects) {
        $SkipProjects = @()
    }


    $ExcludeFilter = ('WebAppTest', $SkipProjects) | %{ "$_.csproj" }

    $TestProjects = Get-ChildItem $TestProjectsLocation -Recurse -Filter '*.csproj' -Exclude $ExcludeFilter |
        %{ $_.FullName }

    $TestProjects | Test-ClientProject -Configuration $Configuration -ToolsetVersion $ToolsetVersion -CI:$CI
}

Function Test-ClientProject {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$TestProjects,
        [string]$Configuration = $DefaultConfiguration,
        [ValidateSet(14,15)]
        [int]$ToolsetVersion = $DefaultMSBuildVersion,
        [switch]$CI
    )
    Process{
        $TestProjects | %{
            $opts = , $_
            $opts += "/t:RunTests", "/p:Configuration=$Configuration;RunTests=true"
            $opts += "/p:VisualStudioVersion=${ToolsetVersion}.0"
            $opts += "/tv:${ToolsetVersion}.0"

            if ($CI) {
                $opts += "/p:TestTargetDir=${ILMergeOutputDir}"
            }

            if (-not $VerbosePreference) {
                $opts += '/verbosity:minimal'
            }

            Trace-Log "$MSBuildExe $opts"
            & $MSBuildExe $opts
            if (-not $?) {
                Error-Log "Tests failed @""$_"". Code: $LASTEXITCODE"
            }
        }
    }
}

Function Read-FileList($FilePath) {
    Get-Content $FilePath | ?{ -not $_.StartsWith('#') } | %{ $_.Trim() } | ?{ $_ -ne '' }
}

# Merges the NuGet.exe
Function Invoke-ILMerge {
    [CmdletBinding()]
    param(
        [string]$InputDir,
        [string]$OutputDir,
        [string]$KeyFile
    )

    $ignoreList = Read-FileList (Join-Path $InputDir '.mergeignore')
    $buildArtifacts = Get-ChildItem $InputDir -Exclude $ignoreList | %{ $_.Name }

    $includeList = Read-FileList (Join-Path $InputDir '.mergeinclude')
    $notInList = $buildArtifacts | ?{ -not ($includeList -contains $_) }
    if ($notInList) {
        Error-Log "Found build artifacts NOT listed in include list: $($notInList -join ', ')"
    }
    $notFound = $includeList | ?{ -not ($buildArtifacts -contains $_) }
    if ($notFound) {
        Error-Log "Missing build artifacts listed in include list: $($notFound -join ', ')"
    }

    # Sort merged assemblies by the order in the .mergeinclude file.
    $buildArtifacts = $includeList + $notInList

    $allowDupList = Read-FileList (Join-Path $InputDir '.mergeallowdup')

    Trace-Log 'Creating the ilmerged nuget.exe'
    $opts = , "$InputDir\NuGet.exe"
    $opts += "/lib:$InputDir"
    $opts += $buildArtifacts
    if ($KeyFile) {
        $opts += "/delaysign"
        $opts += "/keyfile:$KeyFile"
    }

    $opts += "/out:$OutputDir\NuGet.exe"

    foreach ($allowDup in $allowDupList) {
        # /allowDup operates on type name, not namespace and type name
        $typeName = $allowDup.Split('.')[-1]
        $opts += "/allowDup:$typeName"
    }

    if ($VerbosePreference) {
        $opts += '/log'
    }

    Trace-Log "$ILMerge $opts"
    & $ILMerge $opts 2>&1

    if (-not $?) {
        Error-Log "ILMerge has failed. Code: $LASTEXITCODE"
    }
}
