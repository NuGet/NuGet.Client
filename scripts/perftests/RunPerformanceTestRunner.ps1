Param(
    [Parameter(Mandatory = $true)]
    [string]$resultsDirectoryPath,
    [string]$nugetClient,
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

    if ([string]::IsNullOrEmpty($testDirectoryPath)) 
    {
        $testDirectoryPath = $([System.IO.Path]::Combine($env:TEMP, "np"))
    }

    try 
    {
        $testDirectoryPath = GetAbsolutePath $testDirectoryPath
        Log "Discovering the test cases."
        $testFiles = $(Get-ChildItem $PSScriptRoot\testCases "Test-*.ps1" ) | ForEach-Object { $_.FullName }
        $testFiles | ForEach-Object { . $_ $nugetClient $([System.IO.Path]::Combine($testDirectoryPath, "source")) $resultsDirectoryPath $([System.IO.Path]::Combine($testDirectoryPath, "logs")) }
    }
    finally 
    {
        if (!$SkipCleanup) {
            Remove-Item -r -force $testDirectoryPath -ErrorAction Ignore > $null
        }
    }