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

$repoUrl = "https://github.com/NuGet/NuGet.Client.git"
$testCaseName = GenerateNameFromGitUrl $repoUrl
$resultsFilePath = [System.IO.Path]::Combine($resultsFolderPath, "$testCaseName.csv")

if(![string]::IsNullOrEmpty($extraArguments))
{
RunPerformanceTestsOnGitRepository `
    -nugetClientFilePath $nugetClientFilePath `
    -sourceRootFolderPath $sourceRootFolderPath `
    -testCaseName $testCaseName `
    -repoUrl $repoUrl `
    -commitHash "f6279fb833960d9128d16c4e911705167e0bb754" `
    -resultsFilePath $resultsFilePath `
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
    -repoUrl $repoUrl `
    -commitHash "f6279fb833960d9128d16c4e911705167e0bb754" `
    -resultsFilePath $resultsFilePath `
    -logsFolderPath $logsFolderPath `
    -nugetFoldersPath $nugetFoldersPath `
    -iterationCount $iterationCount
}