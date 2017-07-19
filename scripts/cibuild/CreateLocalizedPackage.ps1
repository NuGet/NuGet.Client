<#
.SYNOPSIS
Creates the common localization package for NuGet.

.DESCRIPTION
This script is used to create the common localization package for NuGet.

.PARAMETER BuildRTM
True/false depending on whether nupkgs are being with or without the release labels.

#>

param
(
    [Parameter(Mandatory=$True)]
    [string]$BuildRTM,
    [Parameter(Mandatory=$True)]
    [string]$Version
)


if ($BuildRTM -eq 'false')
{

    $NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
    $LocProjPath = [System.IO.Path]::Combine($NuGetClientRoot, 'build', 'loc.proj')
    $NuGetExe = [System.IO.Path]::Combine($NuGetClientRoot, '.nuget', 'nuget.exe')
    $LocalizationNuspec = [System.IO.Path]::Combine($NuGetClientRoot, 'build', 'Nuspec', 'NuGet.Localization.nuspec')
    $LocalizedFiles = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts', 'LocalizedFiles')
    $OutputPath = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts', 'nupkgs')

    # 1. Move the localized files into a common location.
    Write-Host "Running: $MSBuildExe $LocProjPath /t:MoveLocalizedFilesToLocalizedArtifacts"
    & $MSBuildExe $LocProjPath /t:MoveLocalizedFilesToLocalizedArtifacts

    # 2. Pack the localization package nuspec
    Write-Host "Running: $NuGetExe pack $LocalizationNuspec -properties Version=3.0.0`;LocalizationFilesDirectory=$LocalizedFiles -OutputDirectory $OutputPath"
    & $NuGetExe pack $LocalizationNuspec -properties Version=$Version`;LocalizationFilesDirectory=$LocalizedFiles -OutputDirectory $OutputPath
}