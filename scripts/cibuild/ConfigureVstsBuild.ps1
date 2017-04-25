<#
.SYNOPSIS
Sets build variables during a VSTS build dynamically.

.DESCRIPTION
This script is used to dynamically set some build variables during VSTS build.
Specifically, this script reads the buildcounter.txt file in the $(DropRoot) to
determine the build number of the artifacts, also it sets the $(NupkgOutputDir)
based on whether $(BuildRTM) is true or false.

.PARAMETER BuildCounterFile
Path to the file in the drop root which stores the current build counter.

.PARAMETER BuildInfoJsonFile
Path to the buildInfo.json file that is generated for every build in the output folder.

.PARAMETER BuildRTM
True/false depending on whether nupkgs are being with or without the release labels.

#>

param
(
    [Parameter(Mandatory=$True)]
    [string]$BuildCounterFile,

    [Parameter(Mandatory=$True)]
    [string]$BuildInfoJsonFile,

    [Parameter(Mandatory=$True)]
    [string]$BuildRTM
)

Function Get-Version {
    param(
        [string]$ProductVersion,
        [string]$build
    )
        Write-Host "Evaluating the new VSIX Version : ProductVersion $ProductVersion, build $build"
        # Generate the new minor version: 4.0.0 => 40000, 4.11.5 => 41105. 
        # This assumes we only get to NuGet major/minor 99 at worst, otherwise the logic breaks. 
        #The final version for NuGet 4.0.0, build number 3128 would be 15.0.40000.3128
        $finalVersion = "15.0.$((-join ($ProductVersion -split '\.' | %{ '{0:D2}' -f ($_ -as [int]) } )).TrimStart("0")).$build"    
    
        Write-Host "The new VSIX Version is: $finalVersion"
        return $finalVersion    
}

Function Update-VsixVersion {
    param(
        [string]$ReleaseProductVersion,
        [string]$manifestName,
        [int]$buildNumber
    )
    $vsixManifest = Join-Path $env:BUILD_REPOSITORY_LOCALPATH\src\NuGet.Clients\NuGet.VisualStudio.Client $manifestName

    Write-Host "Updating the VSIX version in manifest $vsixManifest"

    [xml]$xml = get-content $vsixManifest
    $root = $xml.PackageManifest

    # Reading the current version from the manifest
    $oldVersion = $root.Metadata.Identity.Version
    # Evaluate the new version
    $newVersion = Get-Version $ReleaseProductVersion $buildNumber
    Write-Host "Updating the VSIX version [$oldVersion] => [$newVersion]"
    # setting the revision to the new version
    $root.Metadata.Identity.Version = "$newVersion"

    $xml.Save($vsixManifest)

    Write-Host "Updated the VSIX version [$oldVersion] => [$($root.Metadata.Identity.Version)]"
}

$msbuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\bin\msbuild.exe'
if ($BuildRTM -eq 'true')
{
    # Set the $(NupkgOutputDir) build variable in VSTS build
    Write-Host "##vso[task.setvariable variable=NupkgOutputDir;]ReleaseNupkgs"
    $numberOfTries = 0
    do{
        Write-Host "Waiting for buildinfo.json to be generated..."
        $numberOfTries++
        Start-Sleep -s 15
    }
    until ((Test-Path $BuildInfoJsonFile) -or ($numberOfTries -gt 50))
    $json = (Get-Content $BuildInfoJsonFile -Raw) | ConvertFrom-Json
    $currentBuild = [System.Decimal]::Parse($json.BuildNumber)
    # Set the $(Revision) build variable in VSTS build
    Write-Host "##vso[task.setvariable variable=Revision;]$currentBuild" 
}
else
{
    $revision = Get-Content $BuildCounterFile
    $newBuildCounter = [System.Decimal]::Parse($revision)
    $newBuildCounter++
    Set-Content $BuildCounterFile $newBuildCounter
    # Set the $(Revision) build variable in VSTS build
    Write-Host "##vso[task.setvariable variable=Revision;]$newBuildCounter"
    $jsonRepresentation = @{
        BuildNumber = $newBuildCounter
        CommitHash = $env:BUILD_SOURCEVERSION
        BuildBranch = $env:BUILD_SOURCEBRANCHNAME
    }   

    New-Item $BuildInfoJsonFile -Force
    $jsonRepresentation | ConvertTo-Json | Set-Content $BuildInfoJsonFile
    $productVersion = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\config.props /v:m /nologo /t:GetSemanticVersion
    if (-not $?)
    {
        Write-Error "Failed to get product version."
        exit 1
    }
    Update-VsixVersion -manifestName source.extension.vs15.vsixmanifest -ReleaseProductVersion $productVersion -buildNumber $newBuildCounter
    Update-VsixVersion -manifestName source.extension.vs15.insertable.vsixmanifest -ReleaseProductVersion $productVersion -buildNumber $newBuildCounter
}