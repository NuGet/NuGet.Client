Param(
    [Parameter(Mandatory = $True)]
    [string] $nugetClientFilePath,
    [Parameter(Mandatory = $True)]
    [string] $solutionFilePath,
    [Parameter(Mandatory = $True)]
    [string] $resultsFilePath,
    [string] $logsFolderPath,
    [string] $testRootFolderPath,
    [int] $iterationCount = 3,
    [switch] $isPackagesConfig,
    [switch] $skipWarmup,
    [switch] $skipCleanRestores,
    [switch] $skipColdRestores,
    [switch] $skipForceRestores,
    [switch] $skipNoOpRestores
)

. "$PSScriptRoot\PerformanceTestUtilities.ps1"

Function CreateNugetClientArguments(
    [string] $solutionFilePath,
    [string] $nugetClientFilePath,
    [string] $resultsFilePath,
    [string] $logsFolderPath,
    [string] $solutionName,
    [string] $testRunId,
    [string] $scenarioName,
    [string[]] $enabledSwitches)
{
    $arguments = @{
        solutionFilePath = $solutionFilePath
        nugetClientFilePath = $nugetClientFilePath
        resultsFilePath = $resultsFilePath
        logsFolderPath = $logsFolderPath
        scenarioName = $scenarioName
        solutionName = $solutionName
        testRunId = $testRunId
    }

    If ($enabledSwitches -ne $Null)
    {
        ForEach ($enabledSwitch In $enabledSwitches.GetEnumerator())
        {
            $arguments[$enabledSwitch] = $True
        }
    }

    Return $arguments
}

Try
{
    ##### Script logic #####

    If (!(Test-Path $solutionFilePath))
    {
        Log "$solutionFilePath does not exist!" "Red"
        Exit 1
    }

    If (!(Test-Path $nugetClientFilePath))
    {
        Log "$nugetClientFilePath does not exist!" "Red"
        Exit 1
    }

    $nugetClientFilePath = GetAbsolutePath $nugetClientFilePath
    $solutionFilePath = GetAbsolutePath $solutionFilePath
    $resultsFilePath = GetAbsolutePath $resultsFilePath
    $isClientDotnetExe = IsClientDotnetExe $nugetClientFilePath

    If ($isPackagesConfig)
    {
        If ($isClientDotnetExe)
        {
            Log "dotnet.exe does not support packages.config restore." "Red"

            Exit 1
        }

        Log "Restores are expected to be packages.config restores."

        If (!$skipForceRestores)
        {
            Log "Force restore is not supported with packages.config.  Skipping force restores." "Yellow"
            $skipForceRestores = $True
        }
    }

    If (![string]::IsNullOrEmpty($logsFolderPath))
    {
        $logsFolderPath = GetAbsolutePath $logsFolderPath

        If ([System.IO.Path]::GetDirectoryName($resultsFilePath).StartsWith($logsFolderPath))
        {
            Log "$resultsFilePath cannot be under $logsFolderPath" "red"
            Exit 1
        }
    }

    LogDotNetSdkInfo

    if(Test-Path $resultsFilePath)
    {
        Log "The results file $resultsFilePath already exists. The test results of this run will be appended to the same file." "yellow"
    }

    # Setup the NuGet folders - This includes global packages folder/http/plugin caches
    SetupNuGetFolders $nugetClientFilePath $testRootFolderPath

    $processorInfo = GetProcessorInfo

    Log "Measuring restore for $solutionFilePath by $nugetClientFilePath" "Green"

    $solutionName = [System.IO.Path]::GetFileNameWithoutExtension($solutionFilePath)
    $testRunId = [System.DateTime]::UtcNow.ToString("O")

    If (!$skipWarmup)
    {
        Log "Running 1x warmup restore"
        $enabledSwitches = @("cleanGlobalPackagesFolder", "cleanHttpCache", "cleanPluginsCache", "killMSBuildAndDotnetExeProcess")
        If (!$skipForceRestores)
        {
            $enabledSwitches += "force"
        }
        If ($isPackagesConfig)
        {
            $enabledSwitches += "isPackagesConfig"
        }
        $arguments = CreateNugetClientArguments $solutionFilePath $nugetClientFilePath $resultsFilePath $logsFolderPath $solutionName $testRunId "warmup" -enabledSwitches $enabledSwitches
        RunRestore @arguments
    }

    If (!$skipCleanRestores)
    {
        Log "Running $($iterationCount)x clean restores"
        $enabledSwitches = @("cleanGlobalPackagesFolder", "cleanHttpCache", "cleanPluginsCache", "killMSBuildAndDotnetExeProcess")
        If (!$skipForceRestores)
        {
            $enabledSwitches += "force"
        }
        If ($isPackagesConfig)
        {
            $enabledSwitches += "isPackagesConfig"
        }
        $arguments = CreateNugetClientArguments $solutionFilePath $nugetClientFilePath $resultsFilePath $logsFolderPath $solutionName $testRunId "arctic" -enabledSwitches $enabledSwitches
        1..$iterationCount | % { RunRestore @arguments }
    }

    If (!$skipColdRestores)
    {
        Log "Running $($iterationCount)x without a global packages folder"
        $enabledSwitches = @("cleanGlobalPackagesFolder", "killMSBuildAndDotnetExeProcess")
        If (!$skipForceRestores)
        {
            $enabledSwitches += "force"
        }
        If ($isPackagesConfig)
        {
            $enabledSwitches += "isPackagesConfig"
        }
        $arguments = CreateNugetClientArguments $solutionFilePath $nugetClientFilePath $resultsFilePath $logsFolderPath $solutionName $testRunId "cold" -enabledSwitches $enabledSwitches
        1..$iterationCount | % { RunRestore @arguments }
    }

    If (!$skipForceRestores)
    {
        Log "Running $($iterationCount)x force restores"
        $arguments = CreateNugetClientArguments $solutionFilePath $nugetClientFilePath $resultsFilePath $logsFolderPath $solutionName $testRunId "force" -enabledSwitches @("force")
        1..$iterationCount | % { RunRestore @arguments }
    }

    If (!$skipNoOpRestores)
    {
        Log "Running $($iterationCount)x no-op restores"
        $arguments = CreateNugetClientArguments $solutionFilePath $nugetClientFilePath $resultsFilePath $logsFolderPath $solutionName $testRunId "noop"
        1..$iterationCount | % { RunRestore @arguments }
    }

    Log "Completed the performance measurements for $solutionFilePath.  Results are in $resultsFilePath." "green"
}
Finally
{
    CleanNuGetFolders $nugetClientFilePath $testRootFolderPath
}