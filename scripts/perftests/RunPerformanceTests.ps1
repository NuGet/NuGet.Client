Param(
    [Parameter(Mandatory=$true)]
    [string]$nugetClientPath,
    [Parameter(Mandatory=$true)]
    [string]$solutionPath,
    [Parameter(Mandatory=$true)]
    [string]$resultsFilePath,
    [string]$logsPath
)
    . "$PSScriptRoot\PerformanceTestUtilities.ps1"

    # Plugins cache is only available in 4.8+. We need to be careful when using that switch for older clients because it may blow up.
    # The logs location is optional
    function RunRestore([string]$solutionFilePath, [string]$nugetClient, [string]$resultsFile, [string]$logsPath,  
            [switch]$cleanGlobalPackagesFolder, [switch]$cleanHttpCache, [switch]$cleanPluginsCache, [switch]$killMsBuildAndDotnetExeProcesses, [switch]$force)
    {
        if(!(Test-Path $solutionFilePath))
        {
            Log "$solutionFilePath does not exist!" "Red"
            exit 1;
        }

        if(!(Test-Path $nugetClient))
        {
            Log "$nugetClient does not exist!" "Red"
            exit 1;
        }

        Log "Running $nugetClient restore $nugetClient with cleanGlobalPackagesFolder:$cleanGlobalPackagesFolder cleanHttpCache:$cleanHttpCache cleanPluginsCache:$cleanPluginsCache killMsBuildAndDotnetExeProcesses:$killMsBuildAndDotnetExeProcesses force:$force"

        # Do the required cleanup if necesarry
        if($cleanGlobalPackagesFolder -Or $cleanHttpCache -Or $cleanPluginsCache)
        {
            if($cleanGlobalPackagesFolder -And $cleanHttpCache -And $cleanPluginsCache)
            {
                . $nuGetClient locals -clear all -Verbosity quiet
            }
            elseif($cleanGlobalPackagesFolder -And $cleanHttpCache)
            {
                . $nuGetClient locals -clear http-cache global-packages -Verbosity quiet
            }
            elseif($cleanGlobalPackagesFolder)
            {
                . $nuGetClient locals -clear global-packages -Verbosity quiet
            }
            elseif($cleanHttpCache)
            {
                . $nuGetClient locals -clear http-cache -Verbosity quiet
            } 
            else 
            {
                Log "Too risky to invoke a locals clear with the specified parameters."
            }
        }

        if($killMsBuildAndDotnetExeProcesses)
        {
            Stop-Process -name msbuild*,dotnet* -Force
        }


        $start=Get-Date
        $logs = . $nugetClient restore $solutionFilePath -noninteractive $forceArg
        $end=Get-Date
        $totalTime=$end-$start

        if(!$logsPath)
        {
            $logFile = [System.IO.Path]::Combine($logsPath, "restoreLog-$([System.IO.Path]::GetFileNameWithoutExtension($solutionFilePath))-$(get-date -f yyyyMMddTHHmmssffff).txt")
            OutFileWithCreateFolders $logFile $logs
        }

        $globalPackagesFolder = $Env:NUGET_PACKAGES
        if(Test-Path $globalPackagesFolder)
        {
            $gpfNupkgFiles = GetAllPackagesInGlobalPackagesFolder $globalPackagesFolder
            $gpfNupkgsSize = (($gpfNupkgFiles | Measure-Object -property length -sum).Sum/1000000)
            $gpfFiles = GetFiles $globalPackagesFolder
            $gpfFilesSize = (($gpfFiles | Measure-Object -property length -sum).Sum/1000000)
        }
        else 
        {
            Log "The global packages folder $globalPackagesFolder does not exist" "Red"
        }

        $httpCacheFolder = $Env:NUGET_HTTP_CACHE_PATH
        if(Test-Path $httpCacheFolder)
        {
            $httpCacheFiles = GetFiles $httpCacheFolder
            $httpCacheFilesSize = (($httpCacheFiles | Measure-Object -property length -sum).Sum/1000000)
        } 
        else 
        {
            Log "The HTTP cache folder $httpCacheFolder does not exist" "Red"
        }

        $pluginsCacheFolder = $Env:NUGET_PLUGINS_CACHE_PATH
        if(Test-Path $pluginsCacheFolder)
        {
            $pluginsCacheFiles = GetFiles $pluginsCacheFolder
            $pluginsCacheFilesSize = (($pluginsCacheFiles | Measure-Object -property length -sum).Sum/1000000)
        } 
        else 
        {
            Log "The plugins cache folder $httpCacheFolder does not exist" "Yellow"
        }

        if(!(Test-Path $resultsFile)){
            OutFileWithCreateFolders $resultsFile "totalTime,force,globalPackagesFolderNupkgCount,globalPackagesFolderNupkgSize,globalPackagesFolderFilesCount,globalPackagesFolderFilesSize,cleanGlobalPackagesFolder,httpCacheFileCount,httpCacheFilesSize,cleanHttpCache,pluginsCacheFileCount,pluginsCacheFilesSize,cleanPluginsCache,killMsBuildAndDotnetExeProcesses"
        }

        Add-Content -Path $resultsFile -Value "$($totalTime.ToString()),$force,$($gpfNupkgFiles.Count),$gpfNupkgsSize,$($gpfFiles.Count),$gpfFilesSize,$cleanGlobalPackagesFolder,$($httpCacheFiles.Count),$httpCacheFilesSize,$cleanHttpCache,$($pluginsCacheFiles.Count),$pluginsCacheFilesSize,$cleanPluginsCache,$killMsBuildAndDotnetExeProcesses"

        Log "Finished measuring."
    }

    If(![string]::IsNullOrEmpty($logsPath) -And $(GetAbsolutePath $resultsFilePath).StartsWith($(GetAbsolutePath $logsPath)))
    {
        Log "$resultsFilePath cannot be under $logsPath" "red"
        exit(1)
    }

    $nugetClient = GetAbsolutePath $nugetClient
    $solutionFile = GetAbsolutePath $solutionFile
    $resultsFile = GetAbsolutePath $resultsFile

    if(![string]::IsNullOrEmpty($logsPath))
    {
        $logsPath = GetAbsolutePath $logsPath
    }
    
    $iterationCount = 3

    Log "Measuring restore for $solutionFile by $nugetClient" "Green"

    if(Test-Path $resultsFile)
    {
        Log "The results file $resultsFile already exists, deleting it" "yellow"
        & Remove-Item -r $resultsFile -Force
    }

    Log "Running 1x warmup restore"
    RunRestore $solutionFile $nugetClient $resultsFile $logsPath -cleanGlobalPackagesFolder -cleanHttpCache -cleanPluginsCache -killMSBuildAndDotnetExeProcess -force
    Log "Running $($iterationCount)x clean restores"
    1..$iterationCount | % { RunRestore $solutionFile $nugetClient $resultsFile $logsPath -cleanGlobalPackagesFolder -cleanHttpCache -cleanPluginsCache -killMSBuildAndDotnetExeProcess -force }
    Log "Running $($iterationCount)x without a global packages folder"
    1..$iterationCount | % { RunRestore $solutionFile $nugetClient $resultsFile $logsPath -cleanGlobalPackagesFolder -killMSBuildAndDotnetExeProcess -force }
    Log "Running $($iterationCount)x force restores"
    1..$iterationCount | % { RunRestore $solutionFile $nugetClient $resultsFile $logsPath -force }
    Log "Running $($iterationCount)x no-op restores"
    1..$iterationCount | % { RunRestore $solutionFile $nugetClient $resultsFile $logsPath -force }

    Log "Completed the performance measurements for $solutionFile, results are in $resultsFile" "green"

    CleanNuGetFolders $nugetClientPath