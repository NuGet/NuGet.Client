. "$PSScriptRoot\Utils.ps1"
. "$PSScriptRoot\VSUtils.ps1"

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

    While ($currentTestTime -le $EachTestTimoutInSecs)
    {
        Start-Sleep 1
        $currentTestTime++
        $log = (Get-ChildItem $NuGetTestPath -Recurse log.txt | sort LastWriteTime | select -last 1)
        if ($log -and (Test-Path $log.FullName))
        {
            $content = Get-Content $log.FullName
            if (($content.Count -gt 0) -and ($content.Count -gt $currentTestId))
            {
                $currentTestTime = 0

                $content[($currentTestId)..($content.Count - 1)] | %{ Write-Host $_ }

                $lastLine = $content[-1]
                if (($lastLine -is [string]) -and $lastLine.Contains("Tests and/or Test cases, "))
                {
                    # RUN HAS COMPLETED

                    if ($lastLine.Contains(", 0 Failed"))
                    {
                        Write-Host -ForegroundColor Green $lastLine
                    }
                    else
                    {
                        Write-Error -ForegroundColor Red $lastLine
                    }

                    $resultsFile = Join-Path $log.Directory.FullName results.html
                    if (Test-Path $resultsFile)
                    {
                        Start-Process $resultsFile
                        return $resultsFile
                    }
                }
                else
                {
                    $currentTestId = $content.Count
                }
            }
        }
    }

    $errorMessage = 'Run Failed - Results.html did not get created. Completed running of ' + [string]$currentTestId + ' tests. '
                    + $currentTestName + ' was the last test running.' +
                    '. This indicates that the tests did not finish running. It could be that the VS crashed. Please investigate.'

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