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
    [switch]$ValidateVsix,
    [switch]$ValidateSigning
)


if ($BuildRTM -eq 'false')
{   

    $result = 0
    $NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
    $NuGetValidator = [System.IO.Path]::Combine($NuGetClientRoot, 'packages', 'NuGetValidator.1.4.0.2', 'tools', 'NuGetValidator.exe')
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
            Write-Host "Validating NuGet.Client repository signing..."
            Write-Host "Running: $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\sign.proj /v:m /nologo /t:BatchSign"
            $Files = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\sign.proj /v:m /nologo /t:BatchSign
            $FileString = ($files -split "\n") -join ","
            $ArtifactsLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'ArtifactValidation', 'artifacts' )

            
            Write-Host "Running: $NuGetValidator artifact --files @""$FileString"" --output-path $ArtifactsLogOutputDir"
            & $NuGetValidator artifact --files "$FileString" --output-path $ArtifactsLogOutputDir
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
            Write-Host "Validating NuGet.Client repository localization..."
            Write-Host "Running: $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\loc.proj /v:m /nologo /t:BatchLocalize"
            $Files = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\loc.proj /v:m /nologo /t:BatchLocalize
            $FileString = ($files -split "\n") -join ","
            $ArtifactsLogOutputDir = [System.IO.Path]::Combine($BuildOutputTargetPath, 'LocalizationValidation', 'artifacts' )

            Write-Host "Running: $NuGetValidator localization --files @""$FileString"" --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository"

            # We want to exit the process with success even if there are errors.
            & $NuGetValidator localization --files "$FileString" --output-path $ArtifactsLogOutputDir --comments-path $LocalizationRepository
        }
    }

    exit $result
}