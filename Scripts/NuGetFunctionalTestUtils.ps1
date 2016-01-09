. "$PSScriptRoot\Utils.ps1"
. "$PSScriptRoot\VSUtils.ps1"

function ExtractEndToEndZip
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [string]$RootDir)

    $endToEndZipSrc = Join-Path $NuGetDropPath 'EndToEnd.zip'
    $endToEndZip = Join-Path $RootDir 'EndToEnd.zip'

    # Delete the zip file if it exists
    if (Test-Path $endToEndZip)
    {
        Remove-Item $endToEndZip
    }

    Copy-Item $endToEndZipSrc $endToEndZip -Force

    Write-Host 'Creating ' $NuGetTestPath
    mkdir $NuGetTestPath

    ExtractZip $endToEndZip $NuGetTestPath
}

function CopyNuGetCITools
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetCIToolsFolder,
    [Parameter(Mandatory=$true)]
    [string]$NuGetTestPath)

    Write-Host 'Trying to copy necessary tools to ' $NuGetTestPath

    Copy-Item $NuGetCIToolsFolder\*.exe $NuGetTestPath
    Copy-Item $NuGetCIToolsFolder\*.exe.config $NuGetTestPath
    Copy-Item $NuGetCIToolsFolder\*.dll $NuGetTestPath

    Write-Host 'Copied the necessary tools to test path ' $NuGetTestPath
}

function InstallNuGetVSIX
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [string]$NuGetTestPath,
    [Parameter(Mandatory=$true)]
    [string]$NuGetVSIXID,
    [Parameter(Mandatory=$true)]
    [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
    [string]$VSVersion,
    [Parameter(Mandatory=$true)]
    [int]$VSIXInstallerWaitTimeInSecs
    )

    $VSIXSrcPath = Join-Path $NuGetDropPath 'NuGet.Tools.vsix'
    $VSIXPath = Join-Path $RootDir 'NuGet.Tools.vsix'

    Copy-Item $VSIXSrcPath $VSIXPath

    $success = UninstallVSIX $NuGetVSIXID $VSVersion $VSIXInstallerWaitTimeInSecs
    if ($success -eq $false)
    {
        return $false
    }

    $success = InstallVSIX $VSIXPath $VSVersion $VSIXInstallerWaitTimeInSecs
    if ($success -eq $false)
    {
        return $false
    }

    return $true
}

function LaunchVSandGetDTE
{
    param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("15.0", "14.0", "12.0", "11.0", "10.0")]
    [string]$VSVersion,
    [Parameter(Mandatory=$true)]
    $VSLaunchWaitTimeInSecs)

    LaunchVS $VSVersion

    $dte2 = $null
    $count = 0
    $numberOfWaits = 6
    Write-Host "Will wait for $numberOfWaits times and $VSLaunchWaitTimeInSecs seconds each time."

    while($count -lt $numberOfWaits)
    {
        # Wait for $VSLaunchWaitTimeInSecs secs for VS to load before getting the DTE COM object
        Write-Host "Waiting for $VSLaunchWaitTimeInSecs seconds for DTE to become available"
        start-sleep $VSLaunchWaitTimeInSecs

        $dte2 = GetDTE2 $VSVersion
        if ($dte2)
        {
	        break
        }

        $count++
    }

    return $dte2
}

# This function requires a rewrite. This is a first cut
function WaitForResults
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetTestPath,
    [Parameter(Mandatory=$true)]
    $ResultsTotalWaitTimeInSecs,
    [Parameter(Mandatory=$true)]
    $ResultsPollingFrequencyInSecs)

    $sleepCounter = 0
    $totalSleepCycles = [Math]::Ceiling($ResultsTotalWaitTimeInSecs / $ResultsPollingFrequencyInSecs)
    $sleepCycleDuration = [Math]::Min($ResultsTotalWaitTimeInSecs, $ResultsPollingFrequencyInSecs)

    Write-Host 'Started waiting now. Total timeout : ' $ResultsTotalWaitTimeInSecs 'secs.'
    Write-Host 'Number of sleep cycles: ' $totalSleepCycles '. Duration of each sleep cycle: ' $sleepCycleDuration 'secs.'

    While ($sleepCounter -lt $totalSleepCycles)
    {
        # On each cycle, wait for $MaxTimeoutPerCycle seconds
        # and, then check if the Results.html has been created.
        Start-Sleep -Seconds $sleepCycleDuration
        $resultsHtmlFiles = (Get-ChildItem $NuGetTestPath -Recurse Results.html)
        if ($resultsHtmlFiles.Count -eq 1)
        {
            Write-Host 'Found the results html file. Functional tests have completed run.'
            $result = ParseResultsHtml $resultsHtmlFiles[0]
            if ($result[0] -eq $true)
            {
                Write-Host -ForegroundColor Green 'Run passed. Result: ' $result[1]
                return $true
            }

            $errorMessage = 'RUN FAILED. Result: ' + $result[1]
            Write-Error $errorMessage
            return $false
        }

        Write-Host 'Waiting for another ' $sleepCycleDuration 'secs.'
        $sleepCounter++
    }

    $errorMessage = 'Run Failed - Results.html did not get created in timeout ' + $ResultsTotalWaitTimeInSecs + ' secs' +
                    '. This indicates that the tests did not finish running. It could be that the VS crashed. Please investigate."'

    Write-Error $errorMessage
    return $false
}

function ParseResultsHtml
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$resultsHtmlFile)

    $resultsHtmlString = [string]::Join('', (Get-Content $resultsHtmlFile))
    $result = [regex]::matches($resultsHtmlString, 'Ran.*Skipped').Value

    Write-Host 'Parsed Result is ' $result
    if ($result.Contains(", 0 Failed"))
    {
        return @($true, $result)
    }
    return @($false, $result)
}