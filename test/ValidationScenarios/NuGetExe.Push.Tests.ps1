[CmdletBinding()]
param (
    [string]$NuGetApiKey,
    [string]$MyGetApiKey
)

$here = Split-Path -Parent $MyInvocation.MyCommand.Path

. "$here\Common.ps1"

$NuGetExe = "$here\nuget.exe"

Describe "NuGet.exe Push" {
    $NuGetExe | Should Exist
    $NuGetApiKey | Should Not BeNullOrEmpty
    $MyGetApiKey | Should Not BeNullOrEmpty

    It "pushes to local folder" {
        $p = nuget push 'NuGetTestPackage.1.0.0.nupkg' -Source $TestDrive
        $p.StdErr | Should BeNullOrEmpty
        $p.ExitCode | Should Be 0
        "TestDrive:\NuGetTestPackage.1.0.0.nupkg" | Should Exist
    }
    It "pushes to NuGet.org" {
        $p = nuget push 'NuGetTestPackage.1.0.0.nupkg' -ApiKey $NuGetApiKey -Source 'https://www.nuget.org/api/v2/package' -noninteractive -verbosity detailed
        $p.StdErr | Should BeNullOrEmpty
        $p.ExitCode | Should Be 0
    }
    It "pushes to MyGet.org" {
        $p = nuget push 'NuGetTestPackage.1.0.0.nupkg' -ApiKey $MyGetApiKey -Source 'https://www.myget.org/F/nuget-private-test/api/v2/package' -noninteractive -verbosity detailed
        $p.StdErr | Should BeNullOrEmpty
        $p.ExitCode | Should Be 0
    }
    It "cancels after timeout" {
        $p = Invoke-NuGetExe push 'NuGetTestPackage.1.0.0.nupkg' -ApiKey $NuGetApiKey -Source 'https://www.nuget.org/api/v2/package' -timeout 1 -noninteractive
        write-host $p.stderr
        $p.ExitCode | Should Be 1
    }
    It "does NOT push without API key" {
        $p = Invoke-NuGetExe push 'NuGetTestPackage.1.0.0.nupkg' -Source 'https://www.nuget.org/api/v2/package' -noninteractive
        $p.ExitCode | Should Be 1
        $p.stdout | Should Match 'WARNING\: No API Key was provided and no API Key could be found for'
        $p.stderr | Should Match 'Response status code does not indicate success\: 401'
    }
}
