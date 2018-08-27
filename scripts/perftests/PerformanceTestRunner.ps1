Param(
    [Parameter(Mandatory = $true)]
    [string]$resultsDirectoryPath,
    [string[]]$nugetClients,
    [string]$testDirectoryPath,
    [string]$logsDirectoryPath,
    [switch]$SkipCleanup
)

    . "$PSScriptRoot\PerformanceTestUtilities.ps1"

    if (![string]::IsNullOrEmpty($testDirectoryPath) -And $(GetAbsolutePath $resultsDirectoryPath).StartsWith($(GetAbsolutePath $testDirectoryPath))) 
    {
        Log "$resultsDirectoryPath cannot be a subdirectory of $testDirectoryPath" "red"
        exit(1)
    }
    
    if ([string]::IsNullOrEmpty($testDirectoryPath)) 
    {
        $testDirectoryPath = $([System.IO.Path]::Combine($env:TEMP, "np"))
    }

    try 
    {
        foreach($nugetClient in $nugetClients){
            try {
                Log "Running tests for $nugetClient"

                if ([string]::IsNullOrEmpty($nugetClient) -Or !$(Test-Path $nugetClient)) 
                {
                    Log "The NuGet client at '$nugetClient' cannot be resolved. Attempting to use the fallback option." "yellow"
            
                    $relativePath = $([System.IO.Path]::Combine("..", "..", "artifacts", "VS15", "NuGet.exe"))
                    $nugetClient = GetAbsolutePath $([System.IO.Path]::Combine($PSScriptRoot, $relativePath))
                    if (!$(Test-Path $nugetClient)) 
                    {
                        Log "The project has not been built and there is not NuGet.exe available at $nugetClient. Either build the exe or pass a path to a NuGet client."
                        exit(1)
                    }
                    Log "Using the fallback client from $nugetClient"
                }

                $testDirectoryPath = GetAbsolutePath $testDirectoryPath
                Log "Discovering the test cases."
                $testFiles = $(Get-ChildItem $PSScriptRoot\testCases "Test-*.ps1" ) | ForEach-Object { $_.FullName }
                $testFiles | ForEach-Object { 
                    try 
                    {
                    . $_ -nugetClient $nugetClient -sourceRootDirectory $([System.IO.Path]::Combine($testDirectoryPath, "source")) -resultsDirectoryPath $resultsDirectoryPath -logsPath $([System.IO.Path]::Combine($testDirectoryPath, "logs")) 
                    } 
                    catch 
                    {
                        Log "Problem running the test case $_" "red"
                    }
                }
            } 
            catch 
            {
                Log "Problem running the tests with $nugetClient" "red"
            }
        }
    }
    finally 
    {
        if (!$SkipCleanup) {
            Remove-Item -r -force $testDirectoryPath -ErrorAction Ignore > $null
        }
    }