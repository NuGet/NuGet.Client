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
    -repoUrl "https://github.com/dotnet/orleans.git" `
    -commitHash "00fe587cc9d18db3bb238f1e78abf46835b97457" `
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
    -repoUrl "https://github.com/dotnet/orleans.git" `
    -commitHash "00fe587cc9d18db3bb238f1e78abf46835b97457" `
    -resultsFolderPath $resultsFolderPath `
    -logsFolderPath $logsFolderPath `
    -nugetFoldersPath $nugetFoldersPath `
    -iterationCount $iterationCount 
}