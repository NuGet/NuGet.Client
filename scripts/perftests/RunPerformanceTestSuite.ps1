Param(
    [Parameter(Mandatory=$true)]
    [string]$testDirectoryPath,
    [Parameter(Mandatory=$true)]
    [string]$resultsDirectoryPath,
    [string]$nugetClientFilePath,
    [string]$logsDirectoryPath,
    [switch]$SkipCleanup
)

    . "$PSScriptRoot\PerformanceTestUtilities.ps1"

    function RepositorySetup([string]$nuGetClient, [string]$repository, [string]$branch, [string]$sourceDirectoryPath)
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

        . $nuGetClient locals -clear all -Verbosity quiet

        $solutionFile = (Get-ChildItem $sourceDirectoryPath *.sln)[0] | Select-Object -f 1 | Select-Object -ExpandProperty FullName

        Log "Completed the repository setup. The solution file is $solutionFile" "Green"
        return $solutionFile
    }

    function RunPerformanceTest([string]$solutionFile, [string]$resultsFilePath, [string]$logsPath){
        . "$PSScriptRoot\RunPerformanceTests.ps1" $resolvedNuGetClientPath $solutionFile $resultsFilePath $logsPath
    }

    If($(GetAbsolutePath $resultsDirectoryPath).StartsWith($(GetAbsolutePath $testDirectoryPath))){
        Log "$resultsDirectoryPath cannot be a subdirectory of $testDirectoryPath" "red"
        exit(1)
    }

    $testDirectoryPath = GetAbsolutePath $testDirectoryPath
    $logsPath = [System.IO.Path]::Combine($testDirectoryPath,"logs")
    $nugetExeLocations = [System.IO.Path]::Combine($testDirectoryPath,"nugetExe")

    $script:resolvedNuGetClientPath = IIf $(!Test-Path $nugetClientFilePath) $(DownloadNuGetExe 4.7.0 $nugetExeLocations) $nugetClientFilePath

    ### Setup NuGet.Client
    $sourceDirectoryPath = [System.IO.Path]::Combine($testDirectoryPath, "NuGetClient")
    $solutionFile = RepositorySetup $nugetClientPath "https://github.com/NuGet/NuGet.Client.git" "dev" $sourceDirectoryPath
    $resultsFilePath = [System.IO.Path]::Combine($resultsDirectoryPath, "NuGet-Client.csv")

    RunPerformanceTest $solutionFile $resultsFilePath $logsPath

    ### Setup Roslyn

    ### Setup OrchardCore

    if(-not $SkipCleanup){
        Remove-Item -r -force $testDirectoryPath -ErrorAction Ignore
    }