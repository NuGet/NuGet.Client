### Constants ###
$DefaultConfiguration = 'debug'
$DefaultReleaseLabel = 'zlocal'
$DefaultMSBuildVersion = '15'

$NuGetClientRoot = Split-Path -Path $PSScriptRoot -Parent

# allow this to work for scripts/funcTests
if ((Split-Path -Path $PSScriptRoot -Leaf) -eq "scripts") {
    $NuGetClientRoot = Split-Path -Path $NuGetClientRoot -Parent
}

$CLIRoot = Join-Path $NuGetClientRoot 'cli'
$Nupkgs = Join-Path $NuGetClientRoot nupkgs
$Artifacts = Join-Path $NuGetClientRoot artifacts

$DotNetExe = Join-Path $CLIRoot 'dotnet.exe'
$MSBuildRoot = Join-Path ${env:ProgramFiles(x86)} 'MSBuild\'
$MSBuildExeRelPath = 'bin\msbuild.exe'
$NuGetExe = Join-Path $NuGetClientRoot '.nuget\nuget.exe'
$XunitConsole = Join-Path $NuGetClientRoot 'packages\xunit.runner.console.2.1.0\tools\xunit.console.exe'
$ILMerge = Join-Path $NuGetClientRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'

Set-Alias dotnet $DotNetExe
Set-Alias nuget $NuGetExe
Set-Alias xunit $XunitConsole
Set-Alias ilmerge $ILMerge

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

Function Error-Log {
    param(
        [string]$ErrorMessage,
        [switch]$Fatal)
    if (-not $Fatal) {
        Write-Error "[$(Trace-Time)]`t$ErrorMessage"
    }
    else {
        Write-Error "[$(Trace-Time)]`t$ErrorMessage" -ErrorAction Stop
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
        if ($env:TEAMCITY_VERSION) {
            Write-Output "##teamcity[blockOpened name='$BuildStep']"
        }

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
    param()
    $opts = 'submodule', 'update'
    $opts += '--init'
    if (-not $VerbosePreference) {
        $opts += '--quiet'
    }
    Trace-Log 'Updating and initializing submodules'
    Trace-Log "git $opts"
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
    $env:DOTNET_INSTALL_DIR=$NuGetClientRoot

    $installDotnet = Join-Path $CLIRoot "dotnet-install.ps1"

    wget 'https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview2/scripts/obtain/dotnet-install.ps1' -OutFile $installDotnet

    & $installDotnet -Channel preview -i $CLIRoot -Version 1.0.0-preview2-003121

    if (-not (Test-Path $DotNetExe)) {
        Error-Log "Unable to find dotnet.exe. The CLI install may have failed." -Fatal
    }

    # Display build info
    & $DotNetExe --info
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
    $xproject | ConvertTo-Json -Depth 999 | Out-File $fileName
}

# Enables delay signed build
Function Enable-DelaySigning {
    [CmdletBinding()]
    param(
        $MSPFXPath,
        $NuGetPFXPath
    )
    if (Test-Path $MSPFXPath) {
        Trace-Log "Setting NuGet.Core solution to delay sign using $MSPFXPath"
        $env:DNX_BUILD_KEY_FILE=$MSPFXPath
        $env:DNX_BUILD_DELAY_SIGN=$true

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

    if (Test-Path $NuGetPFXPath) {
        Trace-Log "Setting NuGet.Clients solution to delay sign using $NuGetPFXPath"
        $env:NUGET_PFX_PATH= $NuGetPFXPath
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
    Trace-Log 'Cleaning package cache (except the web cache)'

    & nuget locals packages-cache -clear -verbosity detailed
    #& nuget locals global-packages -clear -verbosity detailed
    & nuget locals temp -clear -verbosity detailed
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

    $opts = 'restore', "src\NuGet.Core", "test\NuGet.Core.Tests", "test\NuGet.Core.FuncTests"
    if (-not $VerbosePreference) {
        $opts += '--verbosity', 'minimal'
    }

    Trace-Log "Restoring packages for xprojs"
    Trace-Log "$dotnetExe $opts"
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
            $opts = @()
            if ($VerbosePreference) {
                $opts += '-v'
            }
            $opts += 'pack', $_, '--configuration', $Configuration

            if ($Output) {
                $opts += '--output', (Join-Path $Output (Split-Path $_ -Leaf))
            }

            if($ReleaseLabel -ne 'Release') {
                $opts += '--version-suffix', "${ReleaseLabel}-${BuildNumber}"
            }
            $opts += '--serviceable'
            Trace-Log "$DotNetExe $opts"

            & $DotNetExe $opts
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
    Begin {}
    Process {
        $XProjectLocations | Resolve-Path | %{
            Trace-Log "Running tests in ""$_"""

            $directoryName = Split-Path $_ -Leaf

            pushd $_

            # Check if dnxcore50 exists in the project.json file
            $xtestProjectJson = Join-Path $_ "project.json"
            $xproject = gc $xtestProjectJson -raw | ConvertFrom-Json
            if ($xproject.frameworks.'netcoreapp1.0') {
                # Run tests for Core CLR
                $opts = @()
                if ($VerbosePreference) {
                    $opts += '-v'
                }
                $opts += 'test', '--configuration', $Configuration, '--framework', 'netcoreapp1.0'
                if ($VerbosePreference) {
                    $opts += '-verbose'
                }

                Trace-Log "$DotNetExe $opts"
                & $DotNetExe $opts

                if ($LASTEXITCODE -ne 0) {
                    Error-Log "Tests failed @""$_"" on CoreCLR. Code: $LASTEXITCODE"
                }
            }

            # Run tests for CLR
            if ($xproject.frameworks.net46) {
                # Build
                $opts = @()
                if ($VerbosePreference) {
                    $opts += '-v'
                }
                $opts += 'build', '--configuration', $Configuration, '--runtime', 'win7-x64'

                Trace-Log "$DotNetExe $opts"
                & $DotNetExe $opts

                if ($LASTEXITCODE -ne 0) {
                    Error-Log "Build failed @""$_"" on CLR. Code: $LASTEXITCODE"
                }
                else {
                    $htmlOutput = Join-Path $_ "bin\$Configuration\net46\win7-x64\xunit.results.html"
                    $desktopTestAssembly = Join-Path $_ "bin\$Configuration\net46\win7-x64\$directoryName.dll"
                    $opts = $desktopTestAssembly, '-html', $htmlOutput
                    if ($VerbosePreference) {
                        $opts += '-verbose'
                    }
                    Trace-Log "$XunitConsole $opts"

                    & $XunitConsole $opts
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

Function Test-MSBuildVersionPresent {
    [CmdletBinding()]
    param(
        [string]$MSBuildVersion
    )

   	$MSBuildExe = Get-MSBuildExe $MSBuildVersion

    Test-Path $MSBuildExe
}

Function Get-MSBuildExe {
    param(
        [string]$MSBuildVersion
    )

    $MSBuildExe = Join-Path $MSBuildRoot ($MSBuildVersion + ".0")
    Join-Path $MSBuildExe $MSBuildExeRelPath
}

Function Build-ClientsProjects {
    [CmdletBinding()]
    param(
        [string]$Configuration = $DefaultConfiguration,
        [string]$ReleaseLabel = $DefaultReleaseLabel,
        [int]$BuildNumber = (Get-BuildNumber),
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [switch]$SkipRestore,
        [switch]$Fast
    )

    $solutionPath = Join-Path $NuGetClientRoot NuGet.Clients.sln
    if (-not $SkipRestore) {
        # Restore packages for NuGet.Tooling solution
        Restore-SolutionPackages -path $solutionPath -MSBuildVersion $MSBuildVersion
    }

    # Build the solution
    $opts = , $solutionPath
    $opts += "/p:Configuration=$Configuration;ReleaseLabel=$ReleaseLabel;BuildNumber=$(Format-BuildNumber $BuildNumber)"
    if (-not $VerbosePreference) {
        $opts += '/verbosity:minimal'
    }

    $MSBuildExe = Get-MSBuildExe $MSBuildVersion

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
        [string]$MSBuildVersion = $DefaultMSBuildVersion
    )

    # We don't run command line tests on VS15 as we don't build a nuget.exe for this version
    $testProjectsLocation = Join-Path $NuGetClientRoot test\NuGet.Clients.Tests
    $testProjects = Get-ChildItem $testProjectsLocation -Recurse -Filter '*.csproj' |
        %{ $_.FullName } |
        ?{ -not $_.EndsWith('WebAppTest.csproj') -and
           -not ($_.EndsWith('NuGet.CommandLine.Test.csproj') -and ($MSBuildVersion -eq '15'))}

    $testProjects | Test-ClientProject -Configuration $Configuration -MSBuildVersion $MSBuildVersion
}

Function Test-ClientProject {
    [CmdletBinding()]
    param(
        [parameter(ValueFromPipeline=$True, Mandatory=$True, Position=0)]
        [string[]]$TestProjects,
        [string]$Configuration = $DefaultConfiguration,
        [string]$MSBuildVersion = $DefaultMSBuildVersion
    )
    Begin {}
    Process{
        $TestProjects | %{
            $opts = $_, "/t:RunTests", "/p:Configuration=$Configuration;RunTests=true"
            if (-not $VerbosePreference) {
                $opts += '/verbosity:minimal'
            }

            $MSBuildExe = Get-MSBuildExe $MSBuildVersion

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
        [string]$Configuration = $DefaultConfiguration,
        [string]$MSBuildVersion = $DefaultMSBuildVersion,
        [string]$KeyFile
    )
    $nugetIntermediateExe='NuGet.intermediate.exe'
    $nugetIntermediatePdb='NuGet.intermediate.pdb'
    $nugetCore='NuGet.Core.dll'
    $buildArtifactsFolder = [io.path]::combine($Artifacts, 'NuGet.CommandLine', ($MSBuildVersion + '.0'), $Configuration)
    $ignoreList = Read-FileList (Join-Path $buildArtifactsFolder '.mergeignore')
    $buildArtifacts = Get-ChildItem $buildArtifactsFolder -Exclude $ignoreList | %{ $_.Name }

    $outputFolder = [io.path]::combine($Artifacts, ('VS' + $MSBuildVersion))
    if (-Not (Test-Path $outputFolder)) {
        New-Item -ItemType Directory -Path $outputFolder | Out-Null
    }
    
    $includeList = Read-FileList (Join-Path $buildArtifactsFolder '.mergeinclude')
    $notInList = $buildArtifacts | ?{ -not ($includeList -contains $_) }
    if ($notInList) {
        Error-Log "Found build artifacts NOT listed in include list: $($notInList -join ', ')"
    }
    $notFound = $includeList | ?{ -not ($buildArtifacts -contains $_) }
    if ($notFound) {
        Error-Log "Missing build artifacts listed in include list: $($notFound -join ', ')"
    }

    $nugetIntermediateExePath="$outputFolder\$nugetIntermediateExe"

    Trace-Log 'Creating the intermediate ilmerged nuget.exe'
    $opts = , "$buildArtifactsFolder\NuGet.exe"
    $opts += "/lib:$buildArtifactsFolder"
    $opts += $buildArtifacts
    $opts += "/out:$nugetIntermediateExePath"
    $opts += "/internalize"

    if ($VerbosePreference) {
        $opts += '/log'
    }

    Trace-Log "$ILMerge $opts"
    & $ILMerge $opts 2>&1

    if (-not $?) {
        Error-Log "ILMerge has failed during the intermediate stage. Code: $LASTEXITCODE"
    }

    $opts2 = , "$nugetIntermediateExePath"
    $opts2 += "/lib:$buildArtifactsFolder"
    $opts2 += $nugetCore
    if ($KeyFile) {
        $opts2 += "/delaysign"
        $opts2 += "/keyfile:$KeyFile"
    }

    $opts2 += "/out:$outputFolder\NuGet.exe"

    if ($VerbosePreference) {
        $opts2 += '/log'
    }

    Trace-Log "$ILMerge $opts2"
    & $ILMerge $opts2 2>&1

    if (-not $?) {
        Error-Log "ILMerge has failed. Code: $LASTEXITCODE"
    }

    Remove-Item $nugetIntermediateExePath
    Remove-Item $outputFolder\$nugetIntermediatePdb
}