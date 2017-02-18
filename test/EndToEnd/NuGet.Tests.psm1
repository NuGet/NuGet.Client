$currentPath = Split-Path $MyInvocation.MyCommand.Definition

# Directory where the projects and solutions are created
$testOutputPath = Join-Path $currentPath bin

# Directory where vs templates are located
$templatePath = Join-Path $currentPath ProjectTemplates

# Directory where test scripts are located
$testPath = Join-Path $currentPath tests

$utilityPath = Join-Path $currentPath utility.ps1

# Directory where the test packages are (This is passed to each test method)
$testRepositoryPath = Join-Path $currentPath Packages

$nugetRoot = Join-Path $currentPath "..\.."

$nugetExePath = Join-Path $currentPath "nuget.exe"

if ((Test-Path $nugetExePath) -eq $False)
{
    Write-Host -BackgroundColor Yellow -ForegroundColor Black 'nuget.exe cannot be found at' $nugetExePath
    Write-Host "Downloading nuget.exe"
    wget https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe -OutFile $nugetExePath
}

# Enable NuGet Test Mode
$env:NuGetTestModeEnabled = "True"


$msbuildPath = Join-Path $env:windir Microsoft.NET\Framework\v4.0.30319\msbuild
$testExtensionNames = ( "GenerateTestPackages.exe", "API.Test.dll" )
$testExtensionsRoot = Join-Path $nugetRoot "artifacts\TestExtensions"

$testExtensions = @()

if ((Test-Path $testExtensionsRoot) -eq $True)
{
    $testExtensions  = [System.Collections.ArrayList]($testExtensionNames |
                            %{ Join-Path $testExtensionsRoot $_ })
}
else
{
    # Since the test\TestExtensions folder is not present, assume that the test extensions are present alongside this module script
    $testExtensions  = [System.Collections.ArrayList]($testExtensionNames | %{ Join-Path $currentPath $_ })
}

# Remove GenerateTestPackages alone from the list of test extensions
$generatePackagesExePath = $testExtensions[0]
$testExtensions.RemoveAt(0)

$testExtensions | %{
    if (!(Test-Path $_))
    {
        throw "Test extension $_ is not found. `
If you are running from your dev box, please build your NuGet.Clients solution first. Goodbye!"
    }

    Import-Module $_
}

$VSVersion = [API.Test.VSHelper]::GetVSVersion()

if ($VSVersion.SubString(0, 2) -eq "10")
{
    $targetFrameworkVersion = "v4.0"
}
else
{
    $targetFrameworkVersion = "v4.5"
}

# TODO: Add the ability to rerun failed tests from the previous run

# Add intellisense for the test parameter
Register-TabExpansion 'Run-Test' @{
    'Test' = {
        # Load all of the test scripts
        Get-ChildItem $testPath -Filter *.ps1 | %{
            . $_.FullName
        }

        # Get all of the tests functions
        Get-ChildItem function:\Test* | %{ $_.Name.Substring(5) }
    }
    'File' = {
        # Get all of the tests files
        Get-ChildItem $testPath -Filter *.ps1 | Select-Object -ExpandProperty Name
    }
}

function Rearrange-Tests {
    param($tests)

    if ($VSVersion -eq "12.0" -or $VSVersion -eq "14.0" -or $VSVersion -eq "15.0")
    {
        # Tracked by issue: https://github.com/NuGet/Home/issues/2387
        # And, the commit is linked to the issue
        # TODO: PackageRestore tests should be fixed and enabled or deleted
        # They were only ever running on Dev10.
        $tests = $tests | ? {!($_.Name -like 'Test-PackageRestore*') }
    }

    $tests
}

function global:Run-Test {
    [CmdletBinding(DefaultParameterSetName="Test")]
    param(
        [parameter(ParameterSetName="Test", Position=0)]
        [string]$Test,
        [Parameter(Position=1)]
        [string]$RunId="",
        [parameter(ParameterSetName="File", Mandatory=$true, Position=2)]
        [string]$File,
		[parameter(ParameterSetName="Exclude", Mandatory=$true, Position=2)]
        [string]$Exclude,
        [parameter(Position=3)]
        [bool]$LaunchResultsOnFailure=$true
    )

    Write-Verbose "Loading test extensions modules"

    # Close the solution after every test run
    [API.Test.VSSolutionHelper]::CloseSolution()

    # Load the utility script since we need to use guid
    . $utilityPath

    # Get a reference to the powershell window so we can set focus after the tests are over
    [API.Test.VSHelper]::StorePSWindow()

    if ($RunId)
    {
        $testRunId = $RunId
    }
    else
    {
        $testRunId = New-Guid
    }

    $testRunOutputPath = Join-Path $testOutputPath $testRunId
    $testLogFile = Join-Path $testRunOutputPath log.txt
    $testRealTimeResultsFile = Join-Path $testRunOutputPath Realtimeresults.txt

    # Create the output folder
    mkdir $testRunOutputPath -ErrorAction Ignore | Out-Null

    # Load all of the helper scripts from the current location
    Get-ChildItem $currentPath -Filter *.ps1 | %{
        . $_.FullName $testRunOutputPath $templatePath
    }

    Write-Verbose "Loading scripts from `"$testPath`""

    if (!$File) {
        $File = "*.ps1"
    }

    if ($SourceNuGet -eq $null)
    {
        $SourceNuGet = "nuget.org"
    }

    # Load all of the test scripts
	if (!$Exclude) {
        Get-ChildItem $testPath -Filter $File | %{
        . $_.FullName
		}
	}
	else {
	    Get-ChildItem $testPath -Exclude $Exclude | %{
        . $_.FullName
		}
	}

    # If no tests were specified just run all
    if(!$test) {
        # Get all of the the tests functions
        $tests = Get-ChildItem function:\Test-*
        $tests = Rearrange-Tests $tests
    }
    else {
        $tests = @(Get-ChildItem "function:\Test-$Test")

        if($tests.Count -eq 0) {
            throw "The test `"$Test`" doesn't exist"
        }
    }

    $results = @{}

    # Add a reference to the msbuild assembly in case it isn't there
    Add-Type -AssemblyName "Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL"

    # The vshost that VS launches caues the functional tests to freeze sometimes so disable it
    try
    {
        [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.SetGlobalProperty("UseVSHostingProcess", "false")
    }
    catch
    {
    }

	$startTime = Get-Date
    try {
        # Run all tests
        $testIndex = 0

        $tests | %{
            $testIndex++


            $testObject = $_
            # Trim the Test- prefix
            $testName = $testObject.Name.Substring(5)

            $testCases = @( $null )
            $testCasesInfoString = ".`tThere are not multiple test cases. Just the 1 test"

            try {
                Write-Host 'Getting the test cases factory for ' $testName
                $testCasesFactory = @(Get-ChildItem "function:\TestCases-$testName")
                if($testCasesFactory.Count -gt 1)
                {
                    throw ("There are multiple test case factories for the test " + $testName)
                }

                $testCasesInfoString = $null
                if($testCasesFactory.Count -eq 1)
                {
                    $testCases = & $testCasesFactory[0]
                    $testCasesInfoString = ".`tRunning multiple test cases for test " + $testName + " . Test cases count is " + $testCases.Count
                }
            }
            catch
            {
                $testCases = @( $null )
                $testCasesInfoString = ".`tThere are not multiple test cases. Just the 1 test"
            }

            # Write to log file as we run tests
            "$(Get-Date -format o) Running Test $testName... ($testIndex / $($tests.Count))" + $testCasesInfoString >> $testLogFile
            "Running Test " + $testName + " ($testIndex / $($tests.Count))" + $testCasesInfoString

            $testCaseIndex = 0
            $testCases | %{

                # set name to test name. If this is a test case, we will add that info to the name
                $name = $testName

                $testCaseObject = $_
                if($testCaseObject)
                {
                    $noteProperties = ($testCaseObject.PSObject.Properties | where { $_.MemberType -eq 'NoteProperty' }) | %{ $_.Value }
                    if($noteProperties)
                    {
                        $name += "(" + [system.string]::join("_", $noteProperties) + ")"
                    }
                    $testCaseIndex++
                    "Running Test case $name... ($testCaseIndex / $($testCases.Count))"
                    # Write to log file as we run tests
                    "$(Get-Date -format o) Running Test case $name... ($testCaseIndex / $($testCases.Count))" >> $testLogFile
                }

                $repositoryPath = Join-Path $testRepositoryPath $name

                $values = @{
                    RepositoryRoot = $testRepositoryPath
                    TestRoot = $repositoryPath
                    RepositoryPath = Join-Path $repositoryPath Packages
                    NuGetExe = $nugetExePath
                }

                $generatePackagesExitCode = 0
                if (Test-Path $repositoryPath) {
                    pushd
                    Set-Location $repositoryPath
                    # Generate any packages that might be in the repository dir
                    Get-ChildItem $repositoryPath\* -Include *.dgml,*.nuspec | %{
                        Write-Host 'Running GenerateTestPackages.exe on ' $_.FullName '...'
                        $p = Start-Process $generatePackagesExePath -wait -NoNewWindow -PassThru -ArgumentList $_.FullName
                        if($p.ExitCode -ne 0)
                        {
                            $generatePackagesExitCode = $p.ExitCode
                            Write-Host -ForegroundColor Red 'GenerateTestPackages.exe failed. Exit code is ' + $generatePackagesExitCode
                        }
                        else {
                            Write-Host 'GenerateTestPackages.exe on ' $_.FullName ' succeeded'
                        }
                    }
                    popd
                }

                $context = New-Object PSObject -Property $values

                # Some tests are flaky. We give failed tests another chance to succeed.
                for ($counter = 0; $counter -le 1; $counter++)
                {
                    if ($counter -eq 1)
                    {
                        Write-Host -ForegroundColor Blue "Reruning failed test one more time...."
                    }

                    try {
                        if($generatePackagesExitCode -ne 0)
                        {
                            throw 'GenerateTestPackages.exe failed. Exit code is ' + $generatePackagesExitCode
                        }
                        $executionTime = measure-command { & $testObject $context $testCaseObject }

                        Write-Host -ForegroundColor DarkGreen "Test $name Passed"

                        $results[$name] = New-Object PSObject -Property @{
                            Test = $name
                            Error = $null
                        }

                        $testSucceeded = $true
                    }
                    catch {
                        if($_.Exception.Message.StartsWith("SKIP")) {
                            $message = $_.Exception.Message.Substring(5).Trim()
                            $results[$name] = New-Object PSObject -Property @{
                                Test = $name
                                Error = $message
                                Skipped = $true
                            }

                            Write-Warning "$name was Skipped: $message"
                            $testSucceeded = $true
                        }
                        else {
                            $results[$name] = New-Object PSObject -Property @{
                                Test = $name
                                Error = $_
                            }
                            Write-Host -ForegroundColor Red "$($testObject.InvocationInfo.InvocationName) Failed: $testObject. Exception message: $_"
                            $testSucceeded = $false
                        }
                    }
                    finally {
                        try {
                            # Clear the cache after running each test
                            [NuGet.MachineCache]::Default.Clear()
                        }
                        catch {
                            # The type might not be loaded so don't fail if it isn't
                        }

                        if ($tests.Count -gt 1 -or (!$testSucceeded -and $counter -eq 0)) {
                            [API.Test.VSSolutionHelper]::CloseSolution()
                        }

                        if ($testSucceeded -or $counter -eq 1) {
                            if (Test-Path $repositoryPath) {
                                # Cleanup the output from running the generate packages tool
                                Remove-Item (Join-Path $repositoryPath Packages) -Force -Recurse -ErrorAction SilentlyContinue
                                Remove-Item (Join-Path $repositoryPath Assemblies) -Force -Recurse -ErrorAction SilentlyContinue
                            }
                        }
                    }

                    $results[$name] | Add-Member NoteProperty -Name Time -Value $executionTime
                    $results[$name] | Add-Member NoteProperty -Name Retried -Value ($counter -eq 1)

                    if ($testSucceeded) {
                        break;
                    }
                }

                Append-TextResult $results[$name] $testRealTimeResultsFile
            }
        }
    }
    finally {
		$endTime = Get-Date
		$totalTimeUsed = ($endTime - $startTime).TotalSeconds
		"Total time used $totalTimeUsed seconds" >> $testLogFile

        # Deleting tests
        rm function:\Test*

        # Set focus back to powershell
        [API.Test.VSHelper]::FocusStoredPSWindow()

        Write-TestResults $testRunId $results.Values $testRunOutputPath $testLogFile $LaunchResultsOnFailure

        try
        {
            # Clear out the setting when the tests are done running
            [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.SetGlobalProperty("UseVSHostingProcess", "")
        }
        catch {}
    }
}

function Write-TestResults {
    param(
        $TestRunId,
        $Results,
        $ResultsDirectory,
        $testLogFile,
        $LaunchResultsOnFailure
    )

    # Show failed tests first
    $Results = $Results | Sort-Object -Property Error -Descending

    $HtmlResultPath = Join-Path $ResultsDirectory "Results.html"
    Write-HtmlResults $TestRunId $Results $HtmlResultPath

    $TextResultPath = Join-Path $ResultsDirectory "TestResults.txt"
    Write-TextResults $TestRunId $Results $TextResultPath

    $pass = 0
    $fail = 0
    $skipped = 0

    $rows = $Results | % {
        if($_.Skipped) {
            $skipped++
        }
        elseif($_.Error) {
            $fail++
        }
        else {
            $pass++
        }
    }

    $resultMessage = "Ran $($Results.Count) Tests and/or Test cases, $pass Passed, $fail Failed, $skipped Skipped. See '$HtmlResultPath' or '$TextResultPath' for more details."
    Write-Host $resultMessage
    "$(Get-Date -format o) $resultMessage" >> $testLogFile

    if (($fail -gt 0) -and $LaunchResultsOnFailure -and ($Results.Count -gt 1))
    {
        [System.Diagnostics.Process]::Start($HtmlResultPath)
    }
}

function Get-TextResultRow
{
    param(
        $Result
    )

    $status = 'Passed'

    if($Result.Skipped) {
        $status = 'Skipped'
    }
    elseif($Result.Error) {
        $status = 'Failed'
    }

    $row = "$status $($Result.Test) $([math]::Round($Result.Time.TotalMilliseconds)) $($Result.Error) "

    return $row
}

function Append-TextResult
{
    param(
        $Result,
        $Path
    )

    $row = Get-TextResultRow $Result
    $row >> $Path
}

function Write-TextResults
{
    param(
        $TestRunId,
        $Results,
        $Path
    )

    $rows = $Results | % {
        Get-TextResultRow $_
    }

    $rows | Out-File $Path | Out-Null
}

function Write-HtmlResults
{
    param(
        $TestRunId,
        $Results,
        $Path
    )

    $resultsTemplate = "<html>
    <head>
        <title>
            Test run {0} results
        </title>
        <style>

        body
        {{
            font-family: Trebuchet MS;
            font-size: 0.80em;
            color: #000;
        }}

        a:link, a:visited
        {{
            text-decoration: none;
        }}

        p, ul
        {{
            margin-bottom: 20px;
            line-height: 1.6em;
        }}

        h1, h2, h3, h4, h5, h6
        {{
            font-size: 1.5em;
            color: #000;
            font-family: Arial, Helvetica, sans-serif;
        }}

        h1
        {{
            font-size: 1.8em;
            padding-bottom: 0;
            margin-bottom: 0;
        }}
        h2
        {{
            padding: 0 0 10px 0;
        }}
        table
        {{
            width: 90%;
            border-collapse:collapse;
        }}
        table td
        {{
            padding: 4px;
            border:1px solid #CCC;
        }}
        table th
        {{
            text-align:left;
            border:1px solid #CCC;
        }}
        .Skipped
        {{
            color:black;
            background-color:Yellow;
            font-weight:bold;
        }}
        .Passed
        {{
        }}
        .Failed
        {{
            color:White;
            background-color:Red;
            font-weight:bold;
        }}
        </style>
    </head>
    <body>
        <h2>Test Run {0} ({1})</h2>
        <h3>Ran {2} Tests, {3} Passed, {4} Failed, {5} Skipped</h3>
        <table>
            <tr>
                <th>
                    Result
                </th>
                <th>
                    Test Name
                </th>
                <th>
                    Error Message
                </th>
                <th>
                    Execution time
                </th>
                <th>
                    Retried
                </th>
            </tr>
            {6}
            </table>
    </body>
</html>";

    $testTemplate = "<tr>
    <td class=`"{0}`">{0}</td>
    <td class=`"{0}`">{1}</td>
    <td class=`"{0}`">{2}</td>
    <td class=`"{0}`">{3}</td>
    <td class=`"{0}`">{4}</td>
    </tr>"

    $pass = 0
    $fail = 0
    $skipped = 0

    $rows = $Results | % {
        $status = 'Passed'
        if($_.Skipped) {
            $status = 'Skipped'
            $skipped++
        }
        elseif($_.Error) {
            $status = 'Failed'
            $fail++
        }
        else {
            $pass++
        }

        [String]::Format($testTemplate,
                         $status,
                         [System.Net.WebUtility]::HtmlEncode($_.Test),
                         [System.Net.WebUtility]::HtmlEncode($_.Error),
                         $_.Time.TotalSeconds,
                         $_.Retried)
    }

    [String]::Format($resultsTemplate, $TestRunId, (Split-Path $Path), $Results.Count, $pass, $fail, $skipped, [String]::Join("", $rows)) | Out-File $Path | Out-Null
}

function Get-PackageRepository
{
    param
    (
        $source
    )

    $componentModel = Get-VSComponentModel
    $repositoryProvider = $componentModel.GetService([NuGet.Protocol.Core.Types.ISourceRepositoryProvider])
	$packageSource = New-Object -TypeName NuGet.Configuration.PackageSource -ArgumentList @([System.String]::$source)
    $repositoryProvider.CreateRepository($packageSource)
}