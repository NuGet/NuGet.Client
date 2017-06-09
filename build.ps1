[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [ValidateSet("octopus", "release","rtm", "rc", "rc1", "beta", "beta1", "beta2", "final", "xprivate", "zlocal")]
    [string]$ReleaseLabel = 'zlocal',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$MSPFXPath,
    [string]$NuGetPFXPath,
    [switch]$SkipXProj,
    [Parameter(ParameterSetName='RegularBuild')]
    [switch]$SkipSubModules,
    [Parameter(ParameterSetName='RegularBuild')]
    [switch]$SkipTests,
    [Parameter(ParameterSetName='RegularBuild')]
    [switch]$SkipILMerge,
    [Parameter(ParameterSetName='FastBuild')]
    [switch]$Fast
)

# For TeamCity - Incase any issue comes in this script fail the build. - Be default TeamCity returns exit code of 0 for all powershell even if it fails
trap {
    if ($env:TEAMCITY_VERSION) {
        Write-Host "##teamcity[buildProblem description='$(Format-TeamCityMessage($_.ToString()))']"
    }

    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

function Format-TeamCityMessage([string]$Text) {
    $Text.Replace("|", "||").Replace("'", "|'").Replace("[", "|[").Replace("]", "|]").Replace("`n", "|n").Replace("`r", "|r")
}

$CLIRoot=$PSScriptRoot
$env:DOTNET_INSTALL_DIR=$CLIRoot

. "$PSScriptRoot\build\common.ps1"

$RunTests = (-not $SkipTests) -and (-not $Fast)

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}

$FullBuildNumber = "$PackageReleaseVersion-$ReleaseLabel-$BuildNumber"
Trace-Log "Build $FullBuildNumber started at $startTime"

if ($env:TEAMCITY_VERSION) {
    Write-Host "##teamcity[buildNumber '$(Format-TeamCityMessage("$FullBuildNumber"))']"
}

$BuildErrors = @()

Invoke-BuildStep 'Updating sub-modules' { Update-SubModules } `
    -skip:($SkipSubModules -or $Fast) `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning artifacts' { Clear-Artifacts } `
    -skip:$SkipXProj `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning nupkgs' { Clear-Nupkgs } `
    -skip:$SkipXProj `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Installing dotnet CLI' { Install-DotnetCLI } `
    -ev +BuildErrors

# Restoring tools required for build
Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Enabling delayed signing' {
        param($MSPFXPath, $NuGetPFXPath)
        Enable-DelaySigning $MSPFXPath $NuGetPFXPath
    } `
    -args $MSPFXPath, $NuGetPFXPath `
    -skip:((-not $MSPFXPath) -and (-not $NuGetPFXPath)) `
    -ev +BuildErrors

Invoke-BuildStep 'Building NuGet.Core projects' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast)
        Build-CoreProjects $Configuration $ReleaseLabel $BuildNumber -SkipRestore:$SkipRestore -Fast:$Fast
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast `
    -skip:$SkipXProj `
    -ev +BuildErrors

## Building the VS15 Tooling solution
Invoke-BuildStep 'Building NuGet.Clients projects - VS15 dependencies' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -SkipRestore:$SkipRestore -Fast:$Fast
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore, $Fast `
    -ev +BuildErrors

Invoke-BuildStep 'Creating NuGet.Clients packages - VS15 Toolset' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $MSPFXPath)
        Build-ClientsPackages $Configuration $ReleaseLabel $BuildNumber -KeyFile $MSPFXPath
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $MSPFXPath `
    -skip:($SkipILMerge -or $Fast) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Core tests' {
        param($SkipRestore, $Fast)
        Test-CoreProjects -SkipRestore:$SkipRestore -Fast:$Fast -Configuration $Configuration
    } `
    -args $SkipRestore, $Fast, $Configuration `
    -skip:(-not $RunTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients tests - VS15 dependencies' {
        param($Configuration)
        Test-ClientsProjects -Configuration $Configuration
    } `
    -args $Configuration `
    -skip:((-not $RunTests) -or $SkipVS15) `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}

Write-Host ("`r`n" * 3)
