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

    $repoUrl = "https://github.com/NuGet/NuGet.Client.git"
    $commitHash = "203c517a85791243f53ea08d404ee5b8fae36e35"
    $repoName = GenerateNameFromGitUrl $repoUrl
    $resultsFilePath = [System.IO.Path]::Combine($resultsDirectoryPath, "$repoName.csv")
    $sourcePath = $([System.IO.Path]::Combine($sourceRootDirectory, $repoName))
    $solutionFilePath = SetupGitRepository $repoUrl $commitHash $sourcePath
    # It's fine if this is run from here. It is run again the performance test script, but it'll set it to the same values.
    # Additionally, this will cleanup the extras from the bootstrapping which are already in the local folder, allowing us to get more accurate measurements
    SetupNuGetFolders $nugetClient
    $currentWorkingDirectory = $pwd
    try 
    {
        Set-Location $sourcePath
        . "$sourcePath\configure.ps1" *>>$null
    }
    finally 
    {
        Set-Location $currentWorkingDirectory
    }
. "$PSScriptRoot\..\RunPerformanceTests.ps1" $nugetClient $solutionFilePath $resultsFilePath $logsPath