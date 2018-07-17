Param(
    [Parameter(Mandatory=$true)]
    [string]$resultsDirectoryPath,
    [string]$nugetClientFilePath,
    [string]$testDirectoryPath,
    [string]$logsDirectoryPath,
    [switch]$SkipCleanup
)

    . "$PSScriptRoot\PerformanceTestUtilities.ps1"

    function RepositorySetup([string]$nugetClientFilePath, [string]$repository, [string]$branch, [string]$sourceDirectoryPath)
    {
        if(!(Test-Path $sourceDirectoryPath))
        {
                git clone -b $branch --single-branch $repository $sourceDirectoryPath
        }
        else 
        {
                Log "Skipping the cloning of $repository as $sourceDirectoryPath is not empty" "Yellow"
        }
        $nugetFolders = GetNuGetFoldersPath
        $Env:NUGET_PACKAGES = [System.IO.Path]::Combine($nugetFolders, "gpf")
        $Env:NUGET_HTTP_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "hcp")
        $Env:NUGET_PLUGINS_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "pcp")

        . $nugetClientFilePath locals -clear all -Verbosity quiet

        $solutionFile = (Get-ChildItem $sourceDirectoryPath *.sln)[0] | Select-Object -f 1 | Select-Object -ExpandProperty FullName

        Log "Completed the repository setup. The solution file is $solutionFile" "Green"
        return $solutionFile
    }

    function RunTest([string]$_nugetClientFilePath, [string]$_repositoryUrl, [string]$_branchName, [string]$_resultsDirectoryPath, [string]$_logsPath){
        $repoName = GenerateNameFromGitUrl $_repositoryUrl
        $sourceDirectoryPath = [System.IO.Path]::Combine($testDirectoryPath, $repoName)
        $solutionFile = RepositorySetup $_nugetClientFilePath $_repositoryUrl $_branchName $sourceDirectoryPath
        $resultsFilePath = [System.IO.Path]::Combine($_resultsDirectoryPath, "$repoName.csv")
        . "$PSScriptRoot\RunPerformanceTests.ps1" $_nugetClientFilePath $solutionFile $resultsFilePath $_logsPath
    }

    # The format of the URL is assumed to be https://github.com/NuGet/NuGet.Client.git. The result would be NuGet-Client-git
    function GenerateNameFromGitUrl([string]$gitUrl){
        return $gitUrl.Substring($($gitUrl.LastIndexOf('/') + 1)).Replace('.','-')
    }

    If($(GetAbsolutePath $resultsDirectoryPath).StartsWith($(GetAbsolutePath $testDirectoryPath))){
        Log "$resultsDirectoryPath cannot be a subdirectory of $testDirectoryPath" "red"
        exit(1)
    }

    if([string]::IsNullOrEmpty($testDirectoryPath)){
        $testDirectoryPath = $([System.IO.Path]::Combine($env:TEMP,"np"))
    }

    $testDirectoryPath = GetAbsolutePath $testDirectoryPath
    $logsPath = [System.IO.Path]::Combine($testDirectoryPath,"logs")
    $nugetExeLocations = [System.IO.Path]::Combine($testDirectoryPath,"nugetExe")
    Log "NuGetExeLocations $nugetExeLocations"

    if([string]::IsNullOrEmpty($nugetClientFilePath) -Or !$(Test-Path $nugetClientFilePath))
    {
        $nugetClientFilePath = DownloadNuGetExe 4.7.0 $nugetExeLocations
    }
    Log "Resolved the NuGet Client path to $nugetClientFilePath"

    ### Setup NuGet.Client
    RunTest $nugetClientFilePath "https://github.com/NuGet/NuGet.Client.git" "dev" $resultsDirectoryPath $logsPath

    ### Setup Roslyn
    RunTest $nugetClientFilePath "https://github.com/dotnet/roslyn.git" "master" $resultsDirectoryPath $logsPath

    ### Setup OrchardCore
    RunTest $nugetClientFilePath "https://github.com/OrchardCMS/OrchardCore.git" "dev" $resultsDirectoryPath $logsPath

    if(-not $SkipCleanup){
        Remove-Item -r -force $testDirectoryPath -ErrorAction Ignore > $null
    }