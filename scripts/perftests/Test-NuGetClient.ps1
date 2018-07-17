Param(
    [Parameter(Mandatory=$true)]
    [string]$nugetClientFilePath,
    [Parameter(Mandatory=$true)]
    [string]$resultsDirectoryPath,
    [Parameter(Mandatory=$true)]
    [string]$logsPath
)

    . "$PSScriptRoot\PerformanceTestUtilities.ps1"
    
    RunTest $nugetClientFilePath "https://github.com/NuGet/NuGet.Client.git" "dev" $resultsDirectoryPath $logsPath