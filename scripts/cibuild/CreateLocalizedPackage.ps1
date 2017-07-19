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
    [string]$BuildConfiguration,
    [Parameter(Mandatory=$True)]
    [string]$Version
)

# Declare the output path based on the build type
if ($BuildRTM -eq 'false')
{
    $OutputPath = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts', 'nupkgs')
}
else
{
    $OutputPath = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts', 'ReleaseNupkgs')
}

# Declare common variables
$NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
$LocProjPath = [System.IO.Path]::Combine($NuGetClientRoot, 'build', 'loc.proj')
$NuGetExe = [System.IO.Path]::Combine($NuGetClientRoot, '.nuget', 'nuget.exe')
$LocalizationNuspec = [System.IO.Path]::Combine($NuGetClientRoot, 'build', 'Nuspec', 'NuGet.Localization.nuspec')
$LocalizedFiles = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts', 'LocalizedFiles')
$MSBuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\bin\msbuild.exe'


# 1. Move the localized files into a common location.
Write-Host "Running: $MSBuildExe $LocProjPath /t:MoveLocalizedFilesToLocalizedArtifacts /p:BuildConfiguration=$BuildConfiguration"
& $MSBuildExe $LocProjPath /t:MoveLocalizedFilesToLocalizedArtifacts

if ( Test-Path $LocalizedFiles ) 
{
    # 2. If any localized paths exist then Pack the localization package nuspec
    Write-Host "Running: $NuGetExe pack $LocalizationNuspec -properties Version=$Version`;LocalizationFilesDirectory=$LocalizedFiles -OutputDirectory $OutputPath"
    & $NuGetExe pack $LocalizationNuspec -properties Version=$Version`;LocalizationFilesDirectory=$LocalizedFiles -OutputDirectory $OutputPath
}
