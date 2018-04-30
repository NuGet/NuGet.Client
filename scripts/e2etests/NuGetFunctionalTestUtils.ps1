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

    $parsedResult = Get-ResultFromResultRow $singleResult
    if(-not $parsedResult)
    {
        return $false
    }
    else
    {
        $status = $parsedResult.Status
        $testName = $parsedResult.Name

        $guid = [System.Guid]::NewGuid().ToString("d")
        # The below Write-Host commands are no-oping right now in the release environment.
        Write-Host "##vso[task.logdetail id=$guid;name='$testName';type=build;order=1]Test $testName started"

        if (($status -eq "Failed") -or ($status -eq "Skipped"))
        {
            if ($parts.Length -eq 4)
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
    $lastLogLine = ""
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
                $lastLogLine = $logContent[$currentTestId]
                Write-Host $lastLogLine " and current test time is ${currentTestTime}" 
            }

            $logContent = Get-Content $log
            $logContentLastLine = $logContent[-1]
            $isError = $false
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
                    $isError = $true
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
                if($isError -eq $true)
                {
                    CopyActivityLogToCI
                }
                break
            }
        }
    }

    if ($currentTestTime -gt $EachTestTimoutInSecs)
    {
        $logLineEntries = $lastLogLine -split " "
        $currentTestName = $logLineEntries[2].Replace("...", "") 
        $resultRow = "Failed $currentTestName 600000 Test timed out"
        $resultRow >> $testResults
        $errorMessage = 'Run Failed - Results.html did not get created. ' `
        + 'This indicates that the tests did not finish running. It could be that the VS crashed or a test timed out. Please investigate.'
        CopyResultsToCI $NuGetDropPath $RunCounter $testResults
        CopyActivityLogToCI
        # if($env:CI)
        # {
        ## Running into some hangs, so comment it out for now.
        #     Get-ScreenCapture -OfWindow -OutputPath $env:EndToEndResultsDropPath
        # }
        
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
    [string]$RunCounter,
    [Parameter(Mandatory=$true)]
    [string]$resultsFile)

    $DropPathFileInfo = Get-Item $NuGetDropPath
    $DropPathParent = $DropPathFileInfo.Parent
    $ResultsFileInfo = Get-Item $resultsFile
    $ResultsFileParent = $ResultsFileInfo.Directory
    $EndToEndPath = Join-Path $NuGetDropPath "EndToEnd"
    $FullLogFileName = "FullLog_" + $RunCounter + ".txt"
    $FullLogFilePath = Join-Path $EndToEndPath $FullLogFileName
    $RealTimeResultsFilePath = Join-Path $ResultsFileParent.FullName 'Realtimeresults.txt'

    $TestResultsPath = Join-Path $DropPathParent.FullName 'testresults'
    mkdir $TestResultsPath -ErrorAction Ignore

    $DestinationFileName = 'Run-' + $RunCounter + '-' + (Split-Path $resultsFile -Leaf)
    $DestinationPath = Join-Path $TestResultsPath $DestinationFileName
    Write-Host "Copying results file from $resultsFile to $DestinationPath"
    Copy-Item $resultsFile $DestinationPath
    if($env:CI -and $env:EndToEndResultsDropPath)
    {
        Write-Host "Copying full log file from $FullLogFilePath to $env:EndToEndResultsDropPath"
        if(-not (Test-Path $env:EndToEndResultsDropPath))
        {
            New-Item -Path $env:EndToEndResultsDropPath -ItemType Directory -Force
        }
        Copy-Item $FullLogFilePath -Destination $env:EndToEndResultsDropPath -Force  -ErrorAction SilentlyContinue

        Write-Host "Copying test results file from $resultsFile to $env:EndToEndResultsDropPath"
        Copy-Item $resultsFile -Destination $env:EndToEndResultsDropPath -Force -ErrorAction SilentlyContinue
    }

    OutputResultsForCI -NuGetDropPath $NuGetDropPath -RunCounter $RunCounter -RealTimeResultsFilePath $RealTimeResultsFilePath
}

function CopyActivityLogToCI
{    
    if($env:ActivityLogFullPath) 
    {
        Write-Host "Copying activity log file from $env:ActivityLogFullPath to $env:EndToEndResultsDropPath"
        Copy-Item $env:ActivityLogFullPath -Destination $env:EndToEndResultsDropPath -Force  -ErrorAction SilentlyContinue
    }
}

function OutputResultsForCI
{
    param (
    [Parameter(Mandatory=$true)]
    [string]$NuGetDropPath,
    [Parameter(Mandatory=$true)]
    [string]$RunCounter,
    [Parameter(Mandatory=$true)]
    [string]$RealTimeResultsFilePath)

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
    <failure type="failure"></failure>
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
    $xml.testsuite.file = $className
    $Results = Get-Content $RealTimeResultsFile
    foreach($result in $Results) 
    {   
        $parsedResult = Get-ResultFromResultRow -SingleResult $result
        $newTestCase = $newTestCaseTemplate.clone()
        $newTestCase.classname = $className
        $newTestCase.name = $parsedResult.Name
        $newTestCase.time = $parsedResult.Time
        if($parsedResult.Status -eq "Passed")
        {   #Remove the failure node
            $newTestCase.RemoveChild($newTestCase.ChildNodes[0]) | Out-Null
        }
        else
        {
            $newTestCase.failure.InnerText = $parsedResult.Failure
        }
        $xml.testsuite.AppendChild($newTestCase) > $null
    }   

    # remove users with undefined name (remove template)
    $xml.testsuite.testcase | Where-Object { $_.Name -eq "" } | ForEach-Object  { [void]$xml.testsuite.RemoveChild($_) }
    # save xml to file
    Write-Host "Path: " $XmlResultsFilePath

    $xml.Save($XmlResultsFilePath)
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
                $endIndex = $parts.Length - 1
                $failureMessage = $parts[3..$endIndex]
            }
        }
        $DurationInSeconds = New-TimeSpan -Seconds ($duration/1000.0)
        $result = @{
            Status = $status
            Name = $testName
            Time = $DurationInSeconds.Seconds.ToString()
            Failure = $failureMessage
        }

        return $result
    }
}

function Get-ScreenCapture
{
    param(    
    [Switch]$OfWindow,
    [Parameter(Mandatory=$true)]
    [string]$OutputPath  
    )

    begin {
        Add-Type -AssemblyName System.Drawing
        Add-Type -AssemblyName System.Windows.Forms
        $jpegCodec = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | 
            Where-Object { $_.FormatDescription -eq "JPEG" }
    }
    process {
        Start-Sleep -Milliseconds 250
        if ($OfWindow) {            
            [System.Windows.Forms.Sendkeys]::SendWait("%{PrtSc}")        
        } else {
            [System.Windows.Forms.Sendkeys]::SendWait("{PrtSc}")        
        }
        Start-Sleep -Milliseconds 250
        $bitmap = [Windows.Forms.Clipboard]::GetImage()    
        $ep = New-Object Drawing.Imaging.EncoderParameters  
        $ep.Param[0] = New-Object Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]100)  
        $screenCapturePathBase = "$OutputPath\ScreenCapture"
        $c = 0
        while (Test-Path "${screenCapturePathBase}${c}.jpg") {
            $c++
        }
        $bitmap.Save("${screenCapturePathBase}${c}.jpg", $jpegCodec, $ep)
    }
}