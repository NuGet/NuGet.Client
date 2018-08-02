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

    # Roslyn is difficult to test because they have a script they use for restore.
    # . "$PSScriptRoot\..\PerformanceTestUtilities.ps1"
    
    # $repoUrl = "https://github.com/dotnet/roslyn.git"
    # $commitHash = "1cd08cc27607be7cd025aa65b8527f711d1f53f5" 

    # $repoName = GenerateNameFromGitUrl $repoUrl
    # $resultsFilePath = [System.IO.Path]::Combine($resultsDirectoryPath, "$repoName.csv")
    # $solutionFilePath = SetupGitRepository $repoUrl $commitHash $([System.IO.Path]::Combine($sourceRootDirectory, $repoName))
    # SetupNuGetFolders $nugetClientFilePath
    # $RoslynToolset  = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName(($solutionFilePath), "build", "ToolsetPackages", "RoslynToolset.csproj"))
    # $RepoToolset  = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName(($solutionFilePath), "build", "Targets", "RepoToolset", "Build.csproj"))

    # . $nugetClientFilePath restore $RoslynToolset -noninteractive $forceArg
    # . $nugetClientFilePath restore $RepoToolset -noninteractive $forceArg
    
    # . "$PSScriptRoot\..\RunPerformanceTests.ps1" $nugetClientFilePath $solutionFilePath $resultsFilePath $logsPath 