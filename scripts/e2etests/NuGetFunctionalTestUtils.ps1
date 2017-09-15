. "$PSScriptRoot\Utils.ps1"
. "$PSScriptRoot\VSUtils.ps1"

function EscapeContentForTeamCity
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$content)

    if (!$content) {
        return $content;
    }

    return $content.Replace("|", "||").Replace("'", "|'").Replace("`n", "|r|n").Replace("`r", "|r").Replace("]", "|]");
}

function WriteToTeamCity
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$singleResult)

    if (!$singleResult)
    {
        # If singleResult is null or empty, simply return $false
        return $false
    }

    $parts = $singleResult -split " "

    if ($parts.Length -lt 3)
    {
        Write-Host -ForegroundColor Red "WARNING: PARSING ISSUES. CANNOT WRITE TO TEAM CITY! Result is $singleResult"
        return $false
    }
    else
    {
        $status = $parts[0];
        $testName = $parts[1];
        $duration = $parts[2];

        $guid = [System.Guid]::NewGuid().ToString("d")

        Write-Host "##teamcity[testStarted name='$testName']";
        Write-Host "##vso[task.logdetail id=$guid;name='$testName';type=test;order=1]Test $testName started"

        if (($status -eq "Failed") -or ($status -eq "Skipped"))
        {
            if ($parts.Length -lt 4)
            {
               Write-Host -ForegroundColor Red "WARNING: PARSING ISSUES. CANNOT WRITE TEST FAILURE TO TEAM CITY! Result is $singleResult"
            }
            else
            {
                $result = EscapeContentForTeamCity([string]::Join(" ", ($parts | select -skip 3)))
                if ($status -eq "Failed")
                {
                    Write-Host "##teamcity[testFailed name='$testName' message='$result']"
                    Write-Host "##vso[task.logdetail id=$guid;progress=100;state=Failed]Test $testName failed"
                }
                else
                {
                    Write-Host "##teamcity[testIgnored name='$testName' message='$result']"
                    Write-Host "##vso[task.logdetail id=$guid;progress=100;state=Skipped]Test $testName skipped"
                }
            }
        }

        Write-Host "##teamcity[testFinished name='$testName' duration='$duration']"
        Write-Host "##vso[task.logdetail id=$guid;progress=100;state=Succeeded]Test $testName passed"
    }

    return $true
}

function New-Guid {
    [System.Guid]::NewGuid().ToString("d").Substring(0, 4).Replace("-", "")
}

function Get-Tests
{
    param(
    [Parameter(Mandatory=$true)]
    [string]$NuGetTestPath)

    # Get the path where tests are located
    $testPath = Join-Path $NuGetTestPath tests

    # Load all the test scripts
    Get-ChildItem $testPath -Filter "*.ps1" | %{
        . $_.FullName
    }

    # Get the test methods
    $tests = Get-ChildItem function:\Test-*

    # Apparently, running PackageRestore tests on Dev12 RTM causes hang problem.
    # They have always been disabled. Need to investigate this later.
    $tests = $tests | ? {!($_.Name -like 'Test-PackageRestore*') }

    return $tests
}

# This function requires a rewrite. This is a first cut
function RealTimeLogResults
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetTestPath,
    [Parameter(Mandatory=$true)]
    $EachTestTimoutInSecs)

    $currentTestTime = 0
    $currentTestId = 0
    $currentTestName = [string]$null
    $currentBinFolder = [string]$null

    # Get the current bin folder
    while(!$currentBinFolder -and ($currentTestTime -le $EachTestTimoutInSecs))
    {
        start-sleep 1
        $currentTestTime++
        $currentBinFolder = (ls $NuGetTestPath\bin -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select -First 1)
    }

    if (!$currentBinFolder)
    {
        Write-Error "Looks like no tests were run. There is no folder under $NuGetTestPath\\bin. Please investigate!"
        return $null
    }

    $currentTestTime = 0

    $log = Join-Path $currentBinFolder.FullName "log.txt"
    $testResults = Join-Path $currentBinFolder.FullName "Realtimeresults.txt"

    While ($currentTestTime -le $EachTestTimoutInSecs)
    {
        Start-Sleep 1
        $currentTestTime++
        if ((Test-Path $log) -and (Test-Path $testResults))
        {
            $content = Get-Content $testResults
            if (($content.Count -gt 0) -and ($content.Count -gt $currentTestId))
            {
                $content[($currentTestId)..($content.Count - 1)] | % {
                    $result = WriteToTeamCity $_
                    if ($result -eq $false)
                    {
                        # continues the while loop so that it can be tried again
                        continue
                    }
                    Write-Host $_
                }

                $currentTestTime = 0
                $currentTestId = $content.Count
            }
            else
            {
                Write-Host "Current Test is ${currentTestId} and current test time is ${currentTestTime}"
            }

            $logContent = Get-Content $log
            $logContentLastLine = $logContent[-1]
            if (($logContentLastLine -is [string]) -and $logContentLastLine.Contains("Tests and/or Test cases, ")`
                    -and $content.Count -eq $currentTestId -and $logContent.Length -eq (2 + $currentTestId))
            {
                # RUN HAS COMPLETED

                if ($logContentLastLine.Contains(", 0 Failed"))
                {
                    Write-Host -ForegroundColor Green $logContentLastLine
                }
                else
                {
                    Write-Error $logContentLastLine
                }

                $resultsFile = Join-Path $currentBinFolder results.html
                if (Test-Path $resultsFile)
                {
                    return $resultsFile
                }
                else
                {
                    return $testResults
                }
            }
        }
    }

    $resultsFile = Join-Path $currentBinFolder results.html
    if (Test-Path $resultsFile)
    {
        return $resultsFile
    }

    $errorMessage = 'Run Failed - Results.html did not get created. ' `
    + 'This indicates that the tests did not finish running. It could be that the VS crashed. Please investigate.'

    Write-Error $errorMessage
    return $null
}

function CopyResultsToCI
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [int]$RunCounter,
    [Parameter(Mandatory=$true)]
    [string]$resultsHtmlFile)

    $DropPathFileInfo = Get-Item $NuGetDropPath
    $DropPathParent = $DropPathFileInfo.Parent

    $TestResultsPath = Join-Path $DropPathParent.FullName 'testresults'
    mkdir $TestResultsPath -ErrorAction Ignore

    $DestinationFileName = 'Run-' + $RunCounter + '-Results.html'
    $DestinationPath = Join-Path $TestResultsPath $DestinationFileName
    Copy-Item $resultsHtmlFile $DestinationPath
    Copy-Item $resultsHtmlFile (Join-Path $TestResultsPath 'LatestResults.html')
}