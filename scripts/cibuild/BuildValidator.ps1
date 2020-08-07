<#
.SYNOPSIS
Validates the result of the localization process

.DESCRIPTION
This script is used to validate the results of localization.

.PARAMETER RepoRoot
Path to NuGet.Client repo root folfer

.PARAMETER OutputLogsBasePath
Path to the location where the build artifacts are output

.PARAMETER BuildRTM
True/false depending on whether nupkgs are being with or without the release labels.

.PARAMETER ValidateVsix
Flag to indicate validation on VSIX artifact. Otherwise, validates binaries under $RepoRoot\artifacts folder (default)
#>
param
(
    [Parameter(Mandatory=$True)]
    [string]$RepoRoot,
    [Parameter(Mandatory=$True)]
    [string]$OutputLogsBasePath,
    [Parameter(Mandatory=$True)]
    [string]$BuildRTM,
    [string]$TmpPath = $Env:TEMP,
    [switch]$ValidateVsix
)

if ($BuildRTM -eq 'false')
{   
    $NuGetValidator = [System.IO.Path]::Combine($RepoRoot, 'packages', 'nugetvalidator', '2.0.2', 'tools', 'NuGetValidator.exe')
    $LocalizationRepository = [System.IO.Path]::Combine($RepoRoot, 'submodules', 'NuGet.Build.Localization', 'localize', 'comments', '15')

    if ($ValidateVsix) 
    {
        $VsixLocation = [System.IO.Path]::Combine($RepoRoot, 'artifacts', 'VS15', 'NuGet.Tools.vsix' )
        $VsixExtractLocation = [System.IO.Path]::Combine($TmpPath, 'extractedVsix')
        $VsixLogOutputDir = [System.IO.Path]::Combine($OutputLogsBasePath, 'LocalizationValidation', 'vsix' )

        Write-Host "Validating NuGet.Tools.Vsix localization..."
        Write-Host "Running: $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository"
        & $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository
    }
    else 
    {
        $ArtifactsLocation = [System.IO.Path]::Combine($RepoRoot, 'artifacts')
        $ArtifactsLogOutputDir = [System.IO.Path]::Combine($OutputLogsBasePath, 'LocalizationValidation', 'artifacts' )
    
        Write-Host "Validating NuGet.Client repository localization..."
        Write-Host "Running: $NuGetValidator localization --artifacts-path $ArtifactsLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository"
        & $NuGetValidator localization --artifacts-path $ArtifactsLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository
    }

    # return the exit code from the validator
    exit $LASTEXITCODE
}