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
    [Parameter(Mandatory=$True)]
    [string]$BuildConfiguration,
    [switch]$ValidateVsix,
    [switch]$ValidateSigning
)


if ($BuildRTM -eq 'false')
{   

    $result = 0
    $NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
    $NuGetValidator = [System.IO.Path]::Combine($NuGetClientRoot, 'temp', 'NuGetValidator.1.4.0.3', 'tools', 'NuGetValidator.exe')
    $msbuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\bin\msbuild.exe'
    
    if ($ValidateSigning)
    {
         if ($ValidateVsix) 
        {
            $VsixLocation = [System.IO.Path]::Combine($BuildOutputTargetPath, 'artifacts', 'VS15', 'Insertable', 'NuGet.Tools.vsix' )
            $VsixExtractLocation = [System.IO.Path]::Combine($env:SYSTEM_DEFAULTWORKINGDIRECTORY, 'extractedVsix' ) 
            $VsixLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'ArtifactValidation', 'vsix' )

            Write-Host "Validating NuGet.Tools.Vsix signing..."
            Write-Host "Running: $NuGetValidator artifact --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir"
            & $NuGetValidator artifact --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir
            if (-not $?) {
                $result = 1
            }
        }
        else 
        {
            $FilesListLocation = [System.IO.Path]::Combine($env:SYSTEM_DEFAULTWORKINGDIRECTORY, 'filesListSigning.txt' ) 
            $ArtifactsLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'ArtifactValidation', 'artifacts' )

            Write-Host "Validating NuGet.Client repository signing..."
            Write-Host "Running: $msbuildExe $NuGetClientRoot\build\sign.proj /v:m /nologo /t:BatchSign /p:Configuration=$BuildConfiguration"
            & $msbuildExe $NuGetClientRoot\build\sign.proj /v:m /nologo /t:BatchSign /p:Configuration=$BuildConfiguration > $FilesListLocation
            
            Write-Host "Running: $NuGetValidator artifact --files $FilesListLocation --output-path $ArtifactsLogOutputDir"
            & $NuGetValidator artifact --files-in-file $FilesListLocation --output-path $ArtifactsLogOutputDir
            if (-not $?) {
                $result = 1
            }
        }
    }
    else
    {
        $LocalizationRepository = [System.IO.Path]::Combine($NuGetClientRoot, 'submodules', 'NuGet.Build.Localization', 'localize', 'comments', '15')
        if ($ValidateVsix) 
        {
            $VsixLocation = [System.IO.Path]::Combine($BuildOutputTargetPath, 'artifacts', 'VS15', 'Insertable', 'NuGet.Tools.vsix' )
            $VsixExtractLocation = [System.IO.Path]::Combine($env:SYSTEM_DEFAULTWORKINGDIRECTORY, 'extractedVsix' ) 
            $VsixLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'vsix' )

            Write-Host "Validating NuGet.Tools.Vsix localization..."
            Write-Host "Running: $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository"

            # We want to exit the process with success even if there are errors.
            & $NuGetValidator localization --vsix --vsix-path $VsixLocation --vsix-extract-path $VsixExtractLocation --output-path $VsixLogOutputDir --comments-path $LocalizationRepository
        }
        else 
        {
            $FilesListLocation = [System.IO.Path]::Combine($env:SYSTEM_DEFAULTWORKINGDIRECTORY, 'filesListLocalization.txt' ) 
            $ArtifactsLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'artifacts' )

            Write-Host "Validating NuGet.Client repository localization..."
            Write-Host "Running: $msbuildExe $NuGetClientRoot\build\loc.proj /v:m /nologo /t:BatchLocalize /p:Configuration=$BuildConfiguration"
            $Files = & $msbuildExe $NuGetClientRoot\build\loc.proj /v:m /nologo /t:BatchLocalize /p:Configuration=$BuildConfiguration > $FilesListLocation

            Write-Host "Running: $NuGetValidator localization --files $FilesListLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository"

            # We want to exit the process with success even if there are errors.
            & $NuGetValidator localization --files-in-file $FilesListLocation --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository
        }
    }

    exit $result
}