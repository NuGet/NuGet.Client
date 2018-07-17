Param(
    [Parameter(Mandatory=$true)]
    [string]$resultsDirectoryPath,
    [string]$nugetClientFilePath,
    [string]$testDirectoryPath,
    [string]$logsDirectoryPath,
    [switch]$SkipCleanup
)

    . "$PSScriptRoot\PerformanceTestUtilities.ps1"

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