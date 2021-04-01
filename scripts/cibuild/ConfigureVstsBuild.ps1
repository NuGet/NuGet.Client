<#
.SYNOPSIS
Sets build variables during a VSTS build dynamically.

.DESCRIPTION
This script is used to dynamically set some build variables during VSTS build.
Specifically, this script determines the build number of the artifacts,
also it sets the $(NupkgOutputDir) based on whether $(BuildRTM) is true or false.

.PARAMETER BuildRTM
True/false depending on whether nupkgs are being with or without the release labels.

#>

param
(
    [Parameter(Mandatory=$True)]
    [string]$BuildRTM
)

Function Get-Version {
    param(
        [string]$ProductVersion,
        [string]$build
    )
        Write-Host "Evaluating the new VSIX Version : ProductVersion $ProductVersion, build $build"
        # The major version is NuGetMajorVersion + 11, to match VS's number.
        # The new minor version is: 4.0.0 => 40000, 4.11.5 => 41105. 
        # This assumes we only get to NuGet major/minor/patch 99 at worst, otherwise the logic breaks. 
        # The final version for NuGet 4.0.0, build number 3128 would be 15.0.40000.3128
        $versionParts = $ProductVersion -split '\.'
        $major = $($versionParts[0] / 1) + 11
        $finalVersion = "$major.0.$((-join ($versionParts | %{ '{0:D2}' -f ($_ -as [int]) } )).TrimStart("0")).$build"    
    
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
    Write-Host "##vso[task.setvariable variable=VsixBuildNumber;]$newVersion"
    # setting the revision to the new version
    $root.Metadata.Identity.Version = "$newVersion"

    $xml.Save($vsixManifest)

    Write-Host "Updated the VSIX version [$oldVersion] => [$($root.Metadata.Identity.Version)]"
}

Function Set-RtmLabel {
    param(
        [string]$BuildRTM
    )

    if ($BuildRTM -eq $true) {
        $label = "RTM"
    } else {
        $label = "NonRTM"
    }

    Write-Host "RTM Label: $label"
    Write-Host "##vso[task.setvariable variable=RtmLabel;]$label"
}

Set-RtmLabel -BuildRTM $BuildRTM

$msbuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\bin\msbuild.exe'

# Disable strong name verification of common public keys so that scenarios like building the VSIX or running unit tests
# will not fail because of strong name verification errors.
. "$PSScriptRoot\..\utils\DisableStrongNameVerification.ps1"

$regKeyFileSystem = "HKLM:SYSTEM\CurrentControlSet\Control\FileSystem"
$enableLongPathSupport = "LongPathsEnabled"

$NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
$Submodules = Join-Path $NuGetClientRoot submodules -Resolve

# NuGet.Build.Localization repository set-up
$NuGetLocalization = Join-Path $Submodules NuGet.Build.Localization -Resolve

# Check if there is a localization branch associated with this branch repo 
$currentNuGetBranch = $env:BUILD_SOURCEBRANCHNAME
$lsRemoteOpts = 'ls-remote', 'origin', $currentNuGetBranch
Write-Host "Looking for branch '$currentNuGetBranch' in NuGet.Build.Localization"
$lsResult = & git -C $NuGetLocalization $lsRemoteOpts

if ($lsResult)
{
    $NuGetLocalizationRepoBranch = $currentNuGetBranch
}
else
{
    $NuGetLocalizationRepoBranch = 'dev'
}
Write-Host "NuGet.Build.Localization Branch: $NuGetLocalizationRepoBranch"

# update submodule NuGet.Build.Localization
$updateOpts = 'pull', 'origin', $NuGetLocalizationRepoBranch
Write-Host "git update NuGet.Build.Localization at $NuGetLocalization"
& git -C $NuGetLocalization $updateOpts 2>&1 | Write-Host
# Get the commit of the localization repository that will be used for this build.
$LocalizationRepoCommitHash = & git -C $NuGetLocalization log --pretty=format:'%H' -n 1

if (-not (Test-Path $regKeyFileSystem)) 
{
    Write-Host "Enabling long path support on the build machine"
    Set-ItemProperty -Path $regKeyFileSystem -Name $enableLongPathSupport -Value 1
}


if ($BuildRTM -eq 'true')
{
    # Set the $(NupkgOutputDir) build variable in VSTS build
    Write-Host "##vso[task.setvariable variable=NupkgOutputDir;]ReleaseNupkgs"
    Write-Host "##vso[task.setvariable variable=VsixPublishDir;]VS15-RTM"
}
else
{
    $newBuildCounter = $env:BUILD_BUILDNUMBER
    $VsTargetBranch = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\config.props /v:m /nologo /t:GetVsTargetBranch
    Write-Host $VsTargetBranch
    $jsonRepresentation = @{
        BuildNumber = $newBuildCounter
        CommitHash = $env:BUILD_SOURCEVERSION
        BuildBranch = $env:BUILD_SOURCEBRANCHNAME
        LocalizationRepositoryBranch = $NuGetLocalizationRepoBranch
        LocalizationRepositoryCommitHash = $LocalizationRepoCommitHash
        VsTargetBranch = $VsTargetBranch.Trim()
    }

    # First create the file locally so that we can laster publish it as a build artifact from a local source file instead of a remote source file.
    $localBuildInfoJsonFilePath = [System.IO.Path]::Combine("$Env:BUILD_REPOSITORY_LOCALPATH\artifacts", 'buildinfo.json')

    New-Item $localBuildInfoJsonFilePath -Force
    $jsonRepresentation | ConvertTo-Json | Set-Content $localBuildInfoJsonFilePath

    $productVersion = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\config.props /v:m /nologo /t:GetSemanticVersion
    if (-not $?)
    {
        Write-Error "Failed to get product version."
        exit 1
    }
    Update-VsixVersion -manifestName source.extension.vsixmanifest -ReleaseProductVersion $productVersion -buildNumber $env:BUILDNUMBER
}
