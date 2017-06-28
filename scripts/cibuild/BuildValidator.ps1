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
    $NuGetValidator = [System.IO.Path]::Combine($NuGetClientRoot, 'packages', 'NuGetValidator.1.3.1', 'tools', 'NuGetValidator.exe')
    $ArtifactsLocation = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts')
    $VsixLocation = [System.IO.Path]::Combine($BuildOutputTargetPath, 'artifacts', 'VS15', 'Insertable', 'NuGet.Tools.vsix' )
    $VsixExtractLocation = [System.IO.Path]::Combine($env:SYSTEM_DEFAULTWORKINGDIRECTORY, 'extractedVsix' ) 
    $ArtifactsLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'artifacts' )
    $VsixLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'vsix' )
    $LocalizationRepository = [System.IO.Path]::Combine($NuGetClientRoot, 'submodules', 'NuGet.Build.Localization', 'localize', 'comments', '15')

    Write-Host "Running: $NuGetValidator localization --artifacts-path $ArtifactsLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository"
    & $NuGetValidator localization --artifacts-path $ArtifactsLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository

    # We no longer validate the vsix for localization. Leaving this here, in case this is needed in the future.
    # Write-Host "\n\nRunning: $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository"
    # & $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository
    
    # We want to exit the process with success even if there are errors.
    exit 0
}