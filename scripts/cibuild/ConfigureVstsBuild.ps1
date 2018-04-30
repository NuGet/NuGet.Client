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
    [string]$BuildRTM,
    
    [switch]$SkipUpdateBuildNumber
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
    Write-Host "##vso[task.setvariable variable=VsixBuildNumber;]$newVersion"
    # setting the revision to the new version
    $root.Metadata.Identity.Version = "$newVersion"

    $xml.Save($vsixManifest)

    Write-Host "Updated the VSIX version [$oldVersion] => [$($root.Metadata.Identity.Version)]"
}

$msbuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\bin\msbuild.exe'

# Turn off strong name verification for common DevDiv public keys so that people can execute things against
# test-signed assemblies. One example would be running unit tests on a test-signed assembly during the build.
$regKey = "HKLM:SOFTWARE\Microsoft\StrongName\Verification\*,b03f5f7f11d50a3a"
$regKey32 = "HKLM:SOFTWARE\Wow6432Node\Microsoft\StrongName\Verification\*,b03f5f7f11d50a3a"

$regKeyNuGet = "HKLM:SOFTWARE\Microsoft\StrongName\Verification\*,31bf3856ad364e35"
$regKeyNuGet32 = "HKLM:SOFTWARE\Wow6432Node\Microsoft\StrongName\Verification\*,31bf3856ad364e35"

$has32bitNode = Test-Path "HKLM:SOFTWARE\Wow6432Node"

# update submodule NuGet.Build.Localization
$NuGetClientRoot = $env:BUILD_REPOSITORY_LOCALPATH
$Submodules = Join-Path $NuGetClientRoot submodules -Resolve

$NuGetLocalization = Join-Path $Submodules NuGet.Build.Localization -Resolve
$NuGetLocalizationRepoBranch = 'master'
$updateOpts = 'pull', 'origin', $NuGetLocalizationRepoBranch

Write-Host "git update NuGet.Build.Localization at $NuGetLocalization"
& git -C $NuGetLocalization $updateOpts 2>&1 | Write-Host
# Get the commit of the localization repository that will be used for this build.
$LocalizationRepoCommitHash = & git -C $NuGetLocalization log --pretty=format:'%H' -n 1

if (-not (Test-Path $regKey) -or ($has32bitNode -and -not (Test-Path $regKey32)))
{
    Write-Host "Disabling StrongName Verification so that test-signed binaries can be used on the build machine"
    New-Item -Path (Split-Path $regKey) -Name (Split-Path -Leaf $regKey) -Force | Out-Null

    if ($has32bitNode)
    {
        New-Item -Path (Split-Path $regKey32) -Name (Split-Path -Leaf $regKey32) -Force | Out-Null
    }
}

if (-not (Test-Path $regKeyNuGet) -or ($has32bitNode -and -not (Test-Path $regKeyNuGet32)))
{
    Write-Host "Disabling StrongName Verification for NuGet public key so that test-signed binaries can be used on the build machine"
    New-Item -Path (Split-Path $regKeyNuGet) -Name (Split-Path -Leaf $regKeyNuGet) -Force | Out-Null

    if ($has32bitNode)
    {
        New-Item -Path (Split-Path $regKeyNuGet32) -Name (Split-Path -Leaf $regKeyNuGet32) -Force | Out-Null
    }
}


if ($BuildRTM -eq 'true')
{
    # Set the $(NupkgOutputDir) build variable in VSTS build
    Write-Host "##vso[task.setvariable variable=NupkgOutputDir;]ReleaseNupkgs"
    Write-Host "##vso[task.setvariable variable=VsixPublishDir;]VS15-RTM"
    # Only for backward compatibility with orchestrated builds
    if(-not $SkipUpdateBuildNumber)
    {
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
        Write-Host "##vso[build.updatebuildnumber]$currentBuild" 
        $oldBuildOutputDirectory = Split-Path -Path $BuildInfoJsonFile
        $branchDirectory = Split-Path -Path $oldBuildOutputDirectory
        $newBuildOutputFolder =  Join-Path $branchDirectory $currentBuild
        if(Test-Path $newBuildOutputFolder)
        {
            Move-Item -Path $BuildInfoJsonFile -Destination $newBuildOutputFolder
            Remove-Item -Path $oldBuildOutputDirectory -Force
        }
        else
        {
            Rename-Item $oldBuildOutputDirectory $currentBuild
        }   
    }    
}
else
{
    # Only for backward compatibility with orchestrated builds
    if(-not $SkipUpdateBuildNumber)
    {
        $revision = Get-Content $BuildCounterFile
        $newBuildCounter = [System.Decimal]::Parse($revision)
        $newBuildCounter++
        Set-Content $BuildCounterFile $newBuildCounter
        # Set the $(Revision) build variable in VSTS build
        Write-Host "##vso[task.setvariable variable=Revision;]$newBuildCounter"
        Write-Host "##vso[build.updatebuildnumber]$newBuildCounter"
        Write-Host "##vso[task.setvariable variable=BuildNumber;isOutput=true]$newBuildCounter"
    }
    else
    {
        $newBuildCounter = $env:BUILD_BUILDNUMBER        
    }

    $VsTargetBranch = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\config.props /v:m /nologo /t:GetVsTargetBranch
    $CliTargetBranch = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\config.props /v:m /nologo /t:GetCliTargetBranch
    $SdkTargetBranch = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\config.props /v:m /nologo /t:GetSdkTargetBranch
    Write-Host $VsTargetBranch
    $jsonRepresentation = @{
        BuildNumber = $newBuildCounter
        CommitHash = $env:BUILD_SOURCEVERSION
        BuildBranch = $env:BUILD_SOURCEBRANCHNAME
        LocalizationRepositoryBranch = $NuGetLocalizationRepoBranch
        LocalizationRepositoryCommitHash = $LocalizationRepoCommitHash
        VsTargetBranch = $VsTargetBranch.Trim()
        CliTargetBranch = $CliTargetBranch.Trim()
        SdkTargetBranch = $SdkTargetBranch.Trim()
    }   

    New-Item $BuildInfoJsonFile -Force
    $jsonRepresentation | ConvertTo-Json | Set-Content $BuildInfoJsonFile
    $productVersion = & $msbuildExe $env:BUILD_REPOSITORY_LOCALPATH\build\config.props /v:m /nologo /t:GetSemanticVersion
    if (-not $?)
    {
        Write-Error "Failed to get product version."
        exit 1
    }
    Update-VsixVersion -manifestName source.extension.vs15.vsixmanifest -ReleaseProductVersion $productVersion -buildNumber $env:BUILDNUMBER
}