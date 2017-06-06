<#
.SYNOPSIS
Validates the result of the localization process

.DESCRIPTION
This script is used to validate the results of localization.

.PARAMETER BuildOutputTargetPath
Path to the location where the build artifacts are output

.PARAMETER BuildRTM
True/false depending on whether nupkgs are being with or without the release labels.

#>

param
(
    [Parameter(Mandatory=$True)]
    [string]$BuildOutputTargetPath,
    [Parameter(Mandatory=$True)]
    [string]$BuildRTM
)


if ($BuildRTM -eq 'false')
{    
    $NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
    $LocValidator = [System.IO.Path]::Combine($NuGetClientRoot, 'packages', 'NuGetValidator.Localization.1.2.0', 'tools', 'NuGetValidator.Localization.exe')
    $VsixLocation = [System.IO.Path]::Combine($BuildOutputTargetPath, 'artifacts', 'VS15', 'Insertable', 'NuGet.Tools.vsix' ) 
    $VsixExtractLocation = Join-Path $env:SYSTEM_DEFAULTWORKINGDIRECTORY "extractedVsix"
    $LogOutputDir = Join-Path $BuildOutputTargetPath "LocalizationValidation"
    $LocalizationRepository = [System.IO.Path]::Combine($NuGetClientRoot, 'submodules', 'NuGet.Build.Localization', 'localize', 'comments', '15')
    & $LocValidator $VsixLocation $VsixExtractLocation $LogOutputDir $LocalizationRepository
    # We want to exit the process with success even if there are errors.
    exit 0
}