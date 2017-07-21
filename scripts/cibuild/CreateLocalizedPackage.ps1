<#
.SYNOPSIS
Creates the common localization package for NuGet packages except NuGet.CommandLine.nupkg.

.DESCRIPTION
This script is used to create the common localization package for NuGet.

.PARAMETER BuildRTM
True/false depending on whether nupkgs are being with or without the release labels.

.PARAMETER BuildConfiguration
Build Configuration used for finding the localized files.

.PARAMETER BuildNumber
Build Number used for creating the right package version for pre release package.
#>

param
(
    [Parameter(Mandatory=$True)]
    [string]$BuildRTM,
    [Parameter(Mandatory=$True)]
    [string]$BuildConfiguration,
    [Parameter(Mandatory=$True)]
    [string]$BuildNumber
)

# Localization is not done for $BuildRTM = 'true'
# Here we pack for relelase and prerelease
if ($BuildRTM -eq 'false')
{
    #Same as config.props
    $PackageReleaseVersion = "4.3.0"
    $ReleaseLabel = "rtm"

    $NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
    $LocProjPath = [System.IO.Path]::Combine($NuGetClientRoot, 'build', 'loc.proj')
    $NuGetExe = [System.IO.Path]::Combine($NuGetClientRoot, '.nuget', 'nuget.exe')
    $LocalizationNuspec = [System.IO.Path]::Combine($NuGetClientRoot, 'build', 'Nuspec', 'NuGet.Localization.nuspec')
    $LocalizedFiles = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts', 'LocalizedFiles')
    $NupkgsOutputPath = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts', 'nupkgs')

    # Safe for VSTS CI machines
    $MSBuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\bin\msbuild.exe'

    # Define package versions for pre release and release
    $PackageVersion = "$PackageReleaseVersion-$ReleaseLabel-$BuildNumber"

    # 1. Move the localized files into a common location.
    Write-Host "Running: $MSBuildExe $LocProjPath /t:MoveLocalizedFilesToLocalizedArtifacts /p:Configuration=$BuildConfiguration"
    & $MSBuildExe $LocProjPath /t:MoveLocalizedFilesToLocalizedArtifacts /p:Configuration=$BuildConfiguration

    # 2. If any localized paths exist then Pack the localization package nuspec
    if ( Test-Path $LocalizedFiles ) 
    {
        # Build pre release version
        Write-Host "Running: $NuGetExe pack $LocalizationNuspec -properties Version=$PackageVersion`;LocalizationFilesDirectory=$LocalizedFiles -OutputDirectory $NupkgsOutputPath"
        & $NuGetExe pack $LocalizationNuspec -properties Version=$PackageVersion`;LocalizationFilesDirectory=$LocalizedFiles -OutputDirectory $NupkgsOutputPath
    }
}
