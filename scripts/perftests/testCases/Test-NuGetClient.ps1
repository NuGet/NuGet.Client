Param(
    [Parameter(Mandatory=$true)]
    [string]$nugetClientFilePath,
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
    SetupNuGetFolders $nugetClientFilePath
    $currentWorkingDirectory = $pwd
    cd $sourcePath
    . "$sourcePath\configure.ps1" *>>$null
    cd $currentWorkingDirectory
    . "$PSScriptRoot\..\RunPerformanceTests.ps1" $nugetClientFilePath $solutionFilePath $resultsFilePath $logsPath