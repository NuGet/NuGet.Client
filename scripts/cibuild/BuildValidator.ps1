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
    [string]$BuildRTM,
    [switch]$ValidateVsix
)


if ($BuildRTM -eq 'false')
{   

    $NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
    $NuGetValidator = [System.IO.Path]::Combine($NuGetClientRoot, 'packages', 'NuGetValidator.2.0.0', 'tools', 'NuGetValidator.exe')
    $VsixLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'vsix' )
    $LocalizationRepository = [System.IO.Path]::Combine($NuGetClientRoot, 'submodules', 'NuGet.Build.Localization', 'localize', 'comments', '15')
    
    if ($ValidateVsix) 
    {
        $VsixLocation = [System.IO.Path]::Combine($BuildOutputTargetPath, 'artifacts', 'VS15', 'NuGet.Tools.vsix' )
        $VsixExtractLocation = [System.IO.Path]::Combine($env:SYSTEM_DEFAULTWORKINGDIRECTORY, 'extractedVsix' ) 
        $VsixLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'vsix' )

        Write-Host "Validating NuGet.Tools.Vsix localization..."
        Write-Host "Running: $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository"
        & $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository
    }
    else 
    {
        $ArtifactsLocation = [System.IO.Path]::Combine($NuGetClientRoot, 'artifacts')
        $ArtifactsLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'artifacts' )

        Write-Host "Validating NuGet.Client repository localization..."
        Write-Host "Running: $NuGetValidator localization --artifacts-path $ArtifactsLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository"
        & $NuGetValidator localization --artifacts-path $ArtifactsLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository
    }

    # return the exit code from the validator
    exit $LASTEXITCODE
}