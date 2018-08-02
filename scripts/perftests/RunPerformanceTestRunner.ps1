Param(
    [Parameter(Mandatory=$true)]
    [string]$resultsDirectoryPath,
    [string]$nugetClientFilePath,
    [string]$testDirectoryPath,
    [string]$logsDirectoryPath,
    [switch]$SkipCleanup
)

    . "$PSScriptRoot\PerformanceTestUtilities.ps1"

    If($(GetAbsolutePath $resultsDirectoryPath).StartsWith($(GetAbsolutePath $testDirectoryPath)))
    {
        Log "$resultsDirectoryPath cannot be a subdirectory of $testDirectoryPath" "red"
        exit(1)
    }
    
    if([string]::IsNullOrEmpty($nugetClientFilePath) -Or !$(Test-Path $nugetClientFilePath))
    {
        Log "The NuGet client at '$nugetClientFilePath'cannot be resolved. Attempting to use the fallback option." "yellow"

        $relativePath = $([System.IO.Path]::Combine("..", "..", "..", "artifacts", "VS15", "NuGet.exe"))
        $nugetClientFilePath = GetAbsolutePath $([System.IO.Path]::Combine($(pwd), $relativePath))
        if(!$(Test-Path $nugetClientFilePath))
        {
            Log "The project has not been built and there is not NuGet.exe available at $nugetClientFilePath. Either build the exe or pass a path to a NuGet client."
            exit(1)
        }
    }

    if([string]::IsNullOrEmpty($testDirectoryPath))
    {
        $testDirectoryPath = $([System.IO.Path]::Combine($env:TEMP,"np"))
    }

    $testDirectoryPath = GetAbsolutePath $testDirectoryPath

    Log "Discovering the test cases."
    $testFiles = $(Get-ChildItem $PSScriptRoot\testCases "Test-*.ps1" ) | ForEach-Object { $_.FullName }

    $testFiles | ForEach-Object { . $_ $nugetClientFilePath $([System.IO.Path]::Combine($testDirectoryPath,"source")) $resultsDirectoryPath $([System.IO.Path]::Combine($testDirectoryPath,"logs")) }

    if(-not $SkipCleanup)
    {
        Remove-Item -r -force $testDirectoryPath -ErrorAction Ignore > $null
    }