Param(
    [Parameter(Mandatory = $true)]
    [string] $resultsFolderPath,
    [string[]] $nugetClientFilePaths,
    [string] $testRootFolderPath,
    [string] $logsFolderPath,
    [int] $iterationCount = 3,
    [switch] $skipCleanup
)

. "$PSScriptRoot\PerformanceTestUtilities.ps1"

$resultsFolderPath = GetAbsolutePath $resultsFolderPath
$testRootFolderPath = GetNuGetFoldersPath $testRootFolderPath
$testRootFolderPath = GetAbsolutePath $testRootFolderPath

If ([System.IO.Path]::GetDirectoryName($resultsFolderPath).StartsWith($testRootFolderPath))
{
    Log "$resultsFolderPath cannot be a subdirectory of $testRootFolderPath" "red"

    Exit 1
}

Log "Clients: $nugetClientFilePaths" "green"

Try
{
    ForEach ($nugetClientFilePath In $nugetClientFilePaths)
    {
        Try
        {
            Log "Running tests for $nugetClientFilePath" "green"

            If ([string]::IsNullOrEmpty($nugetClientFilePath) -Or !$(Test-Path $nugetClientFilePath))
            {
                Log "The NuGet client at '$nugetClientFilePath' cannot be resolved." "yellow"

                Exit 1
            }

            Log "Discovering the test cases."
            $testFiles = $(Get-ChildItem $PSScriptRoot\testCases "Test-*.ps1" ) | ForEach-Object { $_.FullName }
            Log "Discovered test cases: $testFiles" "green"

            $testFiles | ForEach-Object {
                $testCase = $_
                Try
                {
                    $sourceRootFolderPath = [System.IO.Path]::Combine($testRootFolderPath, "source")

                    . $_ -nugetClientFilePath $nugetClientFilePath $sourceRootFolderPath $resultsFolderPath $logsFolderPath $testFolderPath $iterationCount
                }
                Catch
                {
                    Log "Problem running the test case $testCase with error $_" "red"
                }
            }
        }
        Catch
        {
            Log "Problem running the tests with $nugetClientFilePath" "red"
        }
    }
}
Finally
{
    If (!$SkipCleanup)
    {
        Remove-Item -r -force $testFolderPath -ErrorAction Ignore > $null
    }
}