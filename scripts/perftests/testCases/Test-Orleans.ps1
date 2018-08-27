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
    RunPerformanceTestsOnGitRepository -nugetClient $nugetClient -sourceRootDirectory $sourceRootDirectory -testCaseName $testCaseName -repoUrl "https://github.com/dotnet/orleans.git" -commitHash "00fe587cc9d18db3bb238f1e78abf46835b97457" -resultsFilePath $resultsFilePath -logsPath $logsPath