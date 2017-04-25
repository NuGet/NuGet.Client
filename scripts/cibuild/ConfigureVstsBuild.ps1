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
    until ((Test-Path $BuildInfoJsonFile) -or ($numberOfTries -gt 10))
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

}