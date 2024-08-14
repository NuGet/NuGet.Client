Param(
    [Parameter(Mandatory = $True)]
    [string] $nugetClientFilePath,
    [Parameter(Mandatory = $True)]
    [string] $sourceRootFolderPath,
    [Parameter(Mandatory = $True)]
    [string] $resultsFolderPath,
    [Parameter(Mandatory = $True)]
    [string] $logsFolderPath,
    [string] $nugetFoldersPath,
    [int] $iterationCount,
    [string] $extraArguments
)

. "$PSScriptRoot\..\PerformanceTestUtilities.ps1"

if(![string]::IsNullOrEmpty($extraArguments))
{
RunPerformanceTestsOnGitRepository `
    -nugetClientFilePath $nugetClientFilePath `
    -sourceRootFolderPath $sourceRootFolderPath `
    -testCaseName $testCaseName `
    -repoUrl "https://github.com/OrchardCMS/OrchardCore.git" `
    -commitHash "991ff7b536811c8ff2c603e30d754b858d009fa2" `
    -resultsFolderPath $resultsFolderPath `
    -logsFolderPath $logsFolderPath `
    -nugetFoldersPath $nugetFoldersPath `
    -iterationCount $iterationCount `
    -extraArguments $extraArguments
} 
Else 
{
RunPerformanceTestsOnGitRepository `
    -nugetClientFilePath $nugetClientFilePath `
    -sourceRootFolderPath $sourceRootFolderPath `
    -testCaseName $testCaseName `
    -repoUrl "https://github.com/OrchardCMS/OrchardCore.git" `
    -commitHash "991ff7b536811c8ff2c603e30d754b858d009fa2" `
    -resultsFolderPath $resultsFolderPath `
    -logsFolderPath $logsFolderPath `
    -nugetFoldersPath $nugetFoldersPath `
    -iterationCount $iterationCount 
}