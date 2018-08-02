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
    function RunRestore([string]$solutionFilePath, [string]$nugetClient, [string]$resultsFile, [string]$logsPath, [string]$runName,
            [switch]$cleanGlobalPackagesFolder, [switch]$cleanHttpCache, [switch]$cleanPluginsCache, [switch]$killMsBuildAndDotnetExeProcesses, [switch]$force)
    {
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
                Log "Too risky to invoke a locals clear with the specified parameters." "yellow"
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
            OutFileWithCreateFolders $resultsFile "name,totalTime,force,globalPackagesFolderNupkgCount,globalPackagesFolderNupkgSize,globalPackagesFolderFilesCount,globalPackagesFolderFilesSize,cleanGlobalPackagesFolder,httpCacheFileCount,httpCacheFilesSize,cleanHttpCache,pluginsCacheFileCount,pluginsCacheFilesSize,cleanPluginsCache,killMsBuildAndDotnetExeProcesses"
        }

        Add-Content -Path $resultsFile -Value "$runName,$($totalTime.ToString()),$force,$($gpfNupkgFiles.Count),$gpfNupkgsSize,$($gpfFiles.Count),$gpfFilesSize,$cleanGlobalPackagesFolder,$($httpCacheFiles.Count),$httpCacheFilesSize,$cleanHttpCache,$($pluginsCacheFiles.Count),$pluginsCacheFilesSize,$cleanPluginsCache,$killMsBuildAndDotnetExeProcesses"

        Log "Finished measuring."
    }

    ##### Script logic #####

    if(!(Test-Path $solutionPath))
    {
        Log "$solutionPath does not exist!" "Red"
        exit 1;
    }

    if(!(Test-Path $nugetClientPath))
    {
        Log "$nugetClientPath does not exist!" "Red"
        exit 1;
    }

    $nugetClientPath = GetAbsolutePath $nugetClientPath
    $solutionPath = GetAbsolutePath $solutionPath
    $resultsFilePath = GetAbsolutePath $resultsFilePath

    if(![string]::IsNullOrEmpty($logsPath))
    {
        $logsPath = GetAbsolutePath $logsPath

        If($resultsFilePath.StartsWith($logsPath))
        {
            Log "$resultsFilePath cannot be under $logsPath" "red"
            exit(1)
        }
    }

    $iterationCount = 3

    Log "Measuring restore for $solutionPath by $nugetClient" "Green"

    if(Test-Path $resultsFilePath)
    {
        Log "The results file $resultsFilePath already exists, deleting it" "yellow"
        & Remove-Item -r $resultsFilePath -Force
    }

    Log "Running 1x warmup restore"
    RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "warmup" -cleanGlobalPackagesFolder -cleanHttpCache -cleanPluginsCache -killMSBuildAndDotnetExeProcess -force
    Log "Running $($iterationCount)x clean restores"
    1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "arctic" -cleanGlobalPackagesFolder -cleanHttpCache -cleanPluginsCache -killMSBuildAndDotnetExeProcess -force }
    Log "Running $($iterationCount)x without a global packages folder"
    1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "cold" -cleanGlobalPackagesFolder -killMSBuildAndDotnetExeProcess -force }
    Log "Running $($iterationCount)x force restores"
    1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "force" -force }
    Log "Running $($iterationCount)x no-op restores"
    1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "noop" -force }

    Log "Completed the performance measurements for $solutionPath, results are in $resultsFilePath" "green"

    CleanNuGetFolders $nugetClientPath