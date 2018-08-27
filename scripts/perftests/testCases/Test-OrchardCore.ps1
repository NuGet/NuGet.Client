Param(
    [Parameter(Mandatory=$true)]
    [string]$nugetClient,
    [Parameter(Mandatory=$true)]
    [string]$sourceRootDirectory,
    [Parameter(Mandatory=$true)]
    [string]$resultsDirectoryPath,
    [Parameter(Mandatory=$true)]
    [string]$logsPath
)


    . "$PSScriptRoot\..\PerformanceTestUtilities.ps1"

    $testCaseName = GenerateNameFromGitUrl $repoUrl
    $resultsFilePath = [System.IO.Path]::Combine($resultsDirectoryPath, "$testCaseName.csv")
    RunPerformanceTestsOnGitRepository -nugetClient $nugetClient -sourceRootDirectory $sourceRootDirectory -testCaseName $testCaseName -repoUrl "https://github.com/OrchardCMS/OrchardCore.git" -commitHash "991ff7b536811c8ff2c603e30d754b858d009fa2" -resultsFilePath $resultsFilePath -logsPath $logsPath