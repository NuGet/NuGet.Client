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

$generatePackagesExePath = Join-Path $currentPath "GenerateTestPackages.exe"
if (-not (Test-Path $generatePackagesExePath)) 
{
    $toolsPath = "$nugetRoot\Tools"
    $generatePackagesProject = Join-Path $toolsPath NuGet\GenerateTestPackages\GenerateTestPackages.csproj
    $generatePackagesExePath = Join-Path $toolsPath NuGet\GenerateTestPackages\bin\Debug\GenerateTestPackages.exe
}

$nugetExePath = Join-Path $currentPath "NuGet.exe"

if (!(Test-Path $nugetExePath)) 
{
    $nugetExePath = "$nugetRoot\test\EndToEnd\NuGet.exe"
}

$msbuildPath = Join-Path $env:windir Microsoft.NET\Framework\v4.0.30319\msbuild

if ($dte.Version.SubString(0, 2) -eq "10")
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

    if ($dte.Version -eq "12.0" -or $dte.Version -eq "14.0")
    {
        # TODO: Running PackageRestore tests on Dev12 RTM causes hang problem,
		# so disable those tests for now.
        $tests = $tests | ? {!($_.Name -like 'Test-PackageRestore*') }
    }

    $tests
}

function global:Run-Test {
    [CmdletBinding(DefaultParameterSetName="Test")]
    param(
        [parameter(ParameterSetName="Test", Position=0)]
        [string]$Test,
        [parameter(ParameterSetName="File", Mandatory=$true, Position=1)]
        [string]$File,
		[parameter(ParameterSetName="Exclude", Mandatory=$true, Position=1)]
        [string]$Exclude,
        [parameter(Position=2)]
        [bool]$LaunchResultsOnFailure=$true
    )
    
    if (!(Test-Path $generatePackagesExePath)) {
        & $msbuildPath $generatePackagesProject /v:quiet /p:TargetFrameworkVersion=$targetFrameworkVersion
    }
    
    # Close the solution after every test run
    $dte.Solution.Close()
    
    # Load the utility script since we need to use guid
    . $utilityPath
    
    # Get a reference to the powershell window so we can set focus after the tests are over
    $window = $dte.ActiveWindow
    
    $testRunId = New-Guid
    $testRunOutputPath = Join-Path $testOutputPath $testRunId
    $testLogFile = Join-Path $testRunOutputPath log.txt
    
    # Create the output folder
    mkdir $testRunOutputPath | Out-Null
       
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
                            $dte.Solution.Close()
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
        $window.SetFocus()
               
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

function Write-TextResults
{
    param(
        $TestRunId,
        $Results,
        $Path
    )

    $rows = $Results | % { 
        $status = 'Passed'
        
        if($_.Skipped) {
            $status = 'Skipped'
        }
        elseif($_.Error) {
            $status = 'Failed'
        }

        "$status $($_.Test) $($_.Error)"
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