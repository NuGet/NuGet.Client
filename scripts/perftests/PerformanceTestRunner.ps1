Param(
    [Parameter(Mandatory = $true)]
    [string] $resultsFolderPath,
    [string[]] $nugetClientFilePaths,
    [string] $testFolderPath,
    [int] $iterationCount = 3,
    [switch] $SkipCleanup
)

. "$PSScriptRoot\PerformanceTestUtilities.ps1"

If ([string]::IsNullOrEmpty($testFolderPath))
{
    $testFolderPath = [System.IO.Path]::Combine($env:TEMP, "np")
}

$resultsFolderPath = GetAbsolutePath $resultsFolderPath
$testFolderPath = GetAbsolutePath $testFolderPath

If (![string]::IsNullOrEmpty($testFolderPath) -And [System.IO.Path]::GetDirectoryName($resultsFolderPath).StartsWith($testFolderPath))
{
    Log "$resultsFolderPath cannot be a subdirectory of $testFolderPath" "red"

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
                    . $_ -nugetClientFilePath $nugetClientFilePath -sourceRootFolderPath $([System.IO.Path]::Combine($testFolderPath, "source")) -resultsFolderPath $resultsFolderPath -logsFolderPath $([System.IO.Path]::Combine($testFolderPath, "logs")) -iterationCount $iterationCount
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