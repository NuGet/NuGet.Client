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

function WriteToCI
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
        # The below Write-Host commands are no-oping right now in the release environment.
        Write-Host "##vso[task.logdetail id=$guid;name='$testName';type=build;order=1]Test $testName started"

        if (($status -eq "Failed") -or ($status -eq "Skipped"))
        {
            if ($parts.Length -lt 4)
            {
               Write-Host -ForegroundColor Red "WARNING: PARSING ISSUES. CANNOT WRITE TEST FAILURE TO TEAM CITY! Result is $singleResult"
            }
            else
            {
                if ($status -eq "Failed")
                {
                    Write-Host "##vso[task.logdetail id=$guid;progress=100;state=Failed]Test $testName failed"
                }
                else
                {
                    Write-Host "##vso[task.logdetail id=$guid;progress=100;state=Skipped]Test $testName skipped"
                }
            }
        }

        Write-Host "##vso[task.logdetail id=$guid;progress=100;state=Succeeded]Test $testName passed"
    }

    return $true
}

function New-Guid {
    [System.Guid]::NewGuid().ToString("d").Substring(0, 4).Replace("-", "")
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
                    $result = $false
                    $contentLine = $_
                    if($content.Count -eq 1)
                    {
                        $result = WriteToCI $content
                        $contentLine = $content
                    }
                    else
                    {
                        $result = WriteToCI $_
                    }
                    
                    if ($result -eq $false)
                    {
                        # continues the while loop so that it can be tried again
                        continue
                    }
                    Write-Host $contentLine
                }

                $currentTestTime = 0
                $currentTestId = $content.Count
            }
            else
            {                               
                $logContent = Get-Content $log
                Write-Host $logContent[$currentTestId] " and current test time is ${currentTestTime}" 
            }

            $logContent = Get-Content $log
            $logContentLastLine = $logContent[-1]
            if (($logContentLastLine -is [string]) -and $logContentLastLine.Contains("Tests and/or Test cases, ")`
                    -and $content.Count -eq $currentTestId)
            {
                # RUN HAS COMPLETED
                Write-Host 'Run has completed. Copying the results file to CI'
                if ($logContentLastLine.Contains(", 0 Failed"))
                {
                    Write-Host -ForegroundColor Green $logContentLastLine
                }
                else
                {
                    Write-Error $logContentLastLine
                }

                $resultsFile = Join-Path $currentBinFolder.FullName results.html
                if (Test-Path $resultsFile)
                {                    
                    CopyResultsToCI $NuGetDropPath $RunCounter $resultsFile
                }
                else
                {
                    CopyResultsToCI $NuGetDropPath $RunCounter $testResults
                }
                break
            }
        }
    }

    if ($currentTestTime -gt $EachTestTimoutInSecs)
    {
        $errorMessage = 'Run Failed - Results.html did not get created. ' `
        + 'This indicates that the tests did not finish running. It could be that the VS crashed or a test timed out. Please investigate.'
        CopyResultsToCI $NuGetDropPath $RunCounter $testResults
        Write-Error $errorMessage
        return $null
    }
}

function CopyResultsToCI
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [int]$RunCounter,
    [Parameter(Mandatory=$true)]
    [string]$resultsFile)

    $DropPathFileInfo = Get-Item $NuGetDropPath
    $DropPathParent = $DropPathFileInfo.Parent
    $ResultsFileInfo = Get-Item $resultsFile
    $ResultsFileParent = $ResultsFileInfo.Parent

    $RealTimeResultsFilePath = Join-Path $ResultsFileParent "Realtimeresults.txt"

    $TestResultsPath = Join-Path $DropPathParent.FullName 'testresults'
    mkdir $TestResultsPath -ErrorAction Ignore

    $DestinationFileName = 'Run-' + $RunCounter + '-' + (Split-Path $resultsFile -Leaf)
    $DestinationPath = Join-Path $TestResultsPath $DestinationFileName
    Write-Host "Copying results file from $resultsFile to $DestinationPath"
    Copy-Item $resultsFile $DestinationPath

    OutputResultsForCI -NuGetDropPath $NuGetDropPath -RunCounter $RunCounter -RealTimeResultsFile $RealTimeResultsFilePath
}

function OutputResultsForCI
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [int]$RunCounter,
    [Parameter(Mandatory=$true)]
    [string]$RealTimeResultsFile)

    $DropPathFileInfo = Get-Item $NuGetDropPath
    $DropPathParent = $DropPathFileInfo.Parent

    $TestResultsPath = Join-Path $DropPathParent.FullName 'testresults'
    $DestinationFileName = 'E2EResults-' + $RunCounter + '.xml'
    $DestinationPath = Join-Path $TestResultsPath $DestinationFileName
    Write-JunitXml -RealTimeResultsFile $RealTimeResultsFilePath -XmlResultsFilePath $DestinationPath
}

Function Write-JunitXml
{
    param (
        [Parameter(Mandatory=$true)]
        [string]$RealTimeResultsFile,
        [Parameter(Mandatory=$true)]
        [string]$XmlResultsFilePath
    )
$template = @'
<testsuite name="" file="">
<testcase classname="" name="" time="">
    <failure type=""></failure>
</testcase>
</testsuite>
'@

    $guid = [System.Guid]::NewGuid().ToString("N")
    $templatePath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), $guid + ".txt");

    $template | Out-File $templatePath -encoding UTF8
    # load template into XML object
    $xml = New-Object xml
    $xml.Load($templatePath)
    # grab template user
    $newTestCaseTemplate = (@($xml.testsuite.testcase)[0]).Clone()  

    $className = "NuGet.Client.EndToEndTests"
    $xml.testsuite.name = $className
    $xml.testsuite.file = $HeaderData.TestFileName
    $Results = Get-Content $RealTimeResults
    foreach($result in $Results) 
    {   
        $parsedResult = Get-ResultFromResultRow -SingleResult $result
        $newTestCase = $newTestCaseTemplate.clone()
        $newTestCase.classname = $className
        $newTestCase.name = $parsedResult.Name
        $newTestCase.time = $parsedResult.Time
        if($result.Result -eq "Passed")
        {   #Remove the failure node
            $newTestCase.RemoveChild($newTestCase.ChildNodes[0]) | Out-Null
        }
        else
        {
            $newTestCase.failure.InnerText = Format-ErrorRecord $parsedResult.Failure
        }
        $xml.testsuite.AppendChild($newTestCase) > $null
    }   

    # remove users with undefined name (remove template)
    $xml.testsuite.testcase | Where-Object { $_.Name -eq "" } | ForEach-Object  { [void]$xml.testsuite.RemoveChild($_) }
    # save xml to file
    Write-Host "Path" $ResultFilePath

    $xml.Save($XmlResultFilePath)

    Remove-Item $templatePath #clean up
}

function Get-ResultFromResultRow
{
    param(
        [Parameter(Mandatory=$true)]
        [string]$SingleResult        
    )

    $parts = $SingleResult -split " "

    if ($parts.Length -lt 3)
    {
        Write-Host -ForegroundColor Red "WARNING: PARSING ISSUES. CANNOT PARSE TEST RESULT: $singleResult"
        return $null
    }
    else
    {
        $status = $parts[0];
        $testName = $parts[1];
        $duration = $parts[2];
        $failureMessage = $null

        if (($status -eq "Failed") -or ($status -eq "Skipped"))
        {
            if ($parts.Length -lt 4)
            {
               Write-Host -ForegroundColor Red "WARNING: PARSING ISSUES. CANNOT WRITE TEST FAILURE:  $singleResult"
            }
            else
            {
                $failureMessage = $parts[3..$parts.Count - 1]
            }
        }

        $result = @{
            Status = $status
            Name = $testName
            Time = $duration
            Failure = $failureMessage
        }

        return $result
    }
}