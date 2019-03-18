Param(
    [Parameter(Mandatory = $true)]
    [string] $resultsFolderPath,
    [string[]] $nugetClientFilePaths,
    [string] $testRootFolderPath,
    [string] $logsFolderPath,
    [int] $iterationCount = 3,
    [bool] $skipRepoCleanup
)

. "$PSScriptRoot\PerformanceTestUtilities.ps1"

$resultsFolderPath = GetAbsolutePath $resultsFolderPath
$testRootFolderPath = GetAbsolutePath $testRootFolderPath
$nugetFoldersPath = GetNuGetFoldersPath $testRootFolderPath
$nugetFoldersPath = GetAbsolutePath $nugetFoldersPath
$sourceRootFolderPath = [System.IO.Path]::Combine($testRootFolderPath, "source")

If ([System.IO.Path]::GetDirectoryName($resultsFolderPath).StartsWith($nugetFoldersPath))
{
    Log "$resultsFolderPath cannot be a subdirectory of $nugetFoldersPath" "red"

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
                    . $_ `
                        -nugetClientFilePath $nugetClientFilePath `
                        -sourceRootFolderPath $sourceRootFolderPath `
                        -resultsFolderPath $resultsFolderPath `
                        -logsFolderPath $logsFolderPath `
                        -nugetFoldersPath $nugetFoldersPath `
                        -iterationCount $iterationCount
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
    Remove-Item -Recurse -Force $nugetFoldersPath -ErrorAction Ignore > $Null

    If (!$skipRepoCleanup)
    {
        Remove-Item -Recurse -Force $sourceRootFolderPath -ErrorAction Ignore > $Null
    }
}