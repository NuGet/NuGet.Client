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

    RunPerformanceTestsOnGitRepository $nugetClient $sourceRootDirectory "https://github.com/dotnet/orleans.git" "00fe587cc9d18db3bb238f1e78abf46835b97457" $resultsDirectoryPath $logsPath