Param(
    [Parameter(Mandatory=$true)]
    [string]$nugetClientPath,
    [Parameter(Mandatory=$true)]
    [string]$solutionPath,
    [Parameter(Mandatory=$true)]
    [string]$resultsFilePath,
    [string]$logsPath,
    [int]$iterationCount = 3,
    [switch]$skipWarmup,
    [switch]$skipCleanRestores,
    [switch]$skipColdRestores,
    [switch]$skipForceRestores,
    [switch]$skipNoOpRestores
)
    . "$PSScriptRoot\PerformanceTestUtilities.ps1"

    # Plugins cache is only available in 4.8+. We need to be careful when using that switch for older clients because it may blow up.
    # The logs location is optional
    function RunRestore([string]$solutionFilePath, [string]$nugetClient, [string]$resultsFile, [string]$logsPath, [string]$restoreName, [string]$testCaseId,
            [switch]$cleanGlobalPackagesFolder, [switch]$cleanHttpCache, [switch]$cleanPluginsCache, [switch]$killMsBuildAndDotnetExeProcesses, [switch]$force)
    {
        Log "Running $nugetClient restore with cleanGlobalPackagesFolder:$cleanGlobalPackagesFolder cleanHttpCache:$cleanHttpCache cleanPluginsCache:$cleanPluginsCache killMsBuildAndDotnetExeProcesses:$killMsBuildAndDotnetExeProcesses force:$force"

        # Do the required cleanup if necesarry
        if($cleanGlobalPackagesFolder -Or $cleanHttpCache -Or $cleanPluginsCache)
        {
            if($cleanGlobalPackagesFolder -And $cleanHttpCache -And $cleanPluginsCache)
            {
                $localsArguments = "all"
            }
            elseif($cleanGlobalPackagesFolder -And $cleanHttpCache)
            {
                $localsArguments =  "http-cache global-packages"
            }
            elseif($cleanGlobalPackagesFolder)
            {
                $localsArguments =  "global-packages"
            }
            elseif($cleanHttpCache)
            {
                $localsArguments = "http-cache"
            } 
            else 
            {
                Log "Too risky to invoke a locals clear with the specified parameters." "yellow"
            }

            if($(IsClientDotnetExe $nugetClient))
            {
                . $nugetClient nuget locals -c $localsArguments *>>$null
            }
            else 
            {
                . $nugetClient locals -clear $localsArguments -Verbosity quiet
            }
        }

        if($killMsBuildAndDotnetExeProcesses)
        {
            Stop-Process -name msbuild*,dotnet* -Force
        }

        $start=Get-Date
        if($(IsClientDotnetExe $nugetClient))
        {
            $logs = . $nugetClient restore $solutionFilePath $forceArg
        }
        else 
        {
            $logs = . $nugetClient restore $solutionFilePath -noninteractive $forceArg
        }
        Log $logs
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
            $globalPackagesFolderNupkgFiles = GetAllPackagesInGlobalPackagesFolder $globalPackagesFolder
            $globalPackagesFolderNupkgsSize = (($globalPackagesFolderNupkgFiles | Measure-Object -property length -sum).Sum/1000000)
            $globalPackagesFolderFiles = GetFiles $globalPackagesFolder
            $globalPackagesFolderFilesSize = (($globalPackagesFolderFiles | Measure-Object -property length -sum).Sum/1000000)
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
        
        $processorDetails = Get-WmiObject Win32_processor
        $cores = $processorDetails | Select-Object -ExpandProperty NumberOfCores
        $logicalCores = $processorDetails | Select-Object -ExpandProperty NumberOfLogicalProcessors
        $processorName = $processorDetails | Select-Object -ExpandProperty Name
        
        $clientName = GetClientName $nugetClient
        $clientVersion = GetClientVersion $nugetClient

        if(!(Test-Path $resultsFile))
        {
            OutFileWithCreateFolders $resultsFile "clientName,clientVersion,testCaseId,name,totalTime,force,globalPackagesFolderNupkgCount,globalPackagesFolderNupkgSize,globalPackagesFolderFilesCount,globalPackagesFolderFilesSize,cleanGlobalPackagesFolder,httpCacheFileCount,httpCacheFilesSize,cleanHttpCache,pluginsCacheFileCount,pluginsCacheFilesSize,cleanPluginsCache,killMsBuildAndDotnetExeProcesses,processorName,cores,logicalCores"
        }

        Add-Content -Path $resultsFile -Value "$clientName,$clientVersion,$testCaseId,$restoreName,$($totalTime.ToString()),$force,$($globalPackagesFolderNupkgFiles.Count),$globalPackagesFolderNupkgsSize,$($globalPackagesFolderFiles.Count),$globalPackagesFolderFilesSize,$cleanGlobalPackagesFolder,$($httpCacheFiles.Count),$httpCacheFilesSize,$cleanHttpCache,$($pluginsCacheFiles.Count),$pluginsCacheFilesSize,$cleanPluginsCache,$killMsBuildAndDotnetExeProcesses,$processorName,$cores,$logicalCores"

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

    # Setup the NuGet folders - This includes global packages folder/http/plugin caches
    SetupNuGetFolders $nugetClientPath

    Log "Measuring restore for $solutionPath by $nugetClientPath" "Green"

    if(Test-Path $resultsFilePath)
    {
        Log "The results file $resultsFilePath already exists, deleting it" "yellow"
        & Remove-Item -r $resultsFilePath -Force
    }

    $uniqueRunID = Get-Date -f d-m-y-h:m:s

    if($skipWarmup)
    {
        Log "Running 1x warmup restore"
        RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "warmup" $uniqueRunID -cleanGlobalPackagesFolder -cleanHttpCache -cleanPluginsCache -killMSBuildAndDotnetExeProcess -force
    }
    if($skipCleanRestores)
    {
        Log "Running $($iterationCount)x clean restores"
        1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "arctic" $uniqueRunID -cleanGlobalPackagesFolder -cleanHttpCache -cleanPluginsCache -killMSBuildAndDotnetExeProcess -force }
    }
    if($skipColdRestores)
    {
        Log "Running $($iterationCount)x without a global packages folder"
        1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "cold" $uniqueRunID -cleanGlobalPackagesFolder -killMSBuildAndDotnetExeProcess -force }
    }
    if($skipForceRestores)
    {
        Log "Running $($iterationCount)x force restores"
        1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "force" $uniqueRunID -force }
    }
    if($skipNoOpRestores){
        Log "Running $($iterationCount)x no-op restores"
        1..$iterationCount | % { RunRestore $solutionPath $nugetClientPath $resultsFilePath $logsPath "noop" $uniqueRunID -force }
    }
    Log "Completed the performance measurements for $solutionPath, results are in $resultsFilePath" "green"

    # Clean the NuGet folders.
    CleanNuGetFolders $nugetClientPath