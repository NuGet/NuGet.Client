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

    RunPerformanceTestsOnGitRepository $nugetClient $sourceRootDirectory "https://github.com/OrchardCMS/OrchardCore.git" "991ff7b536811c8ff2c603e30d754b858d009fa2" $resultsDirectoryPath $logsPath
