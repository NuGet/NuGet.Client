<#
.SYNOPSIS
Build and run unit-tests and functional tests.

.PARAMETER Configuration
Build configuration (debug by default)

.PARAMETER ReleaseLabel
Release label to use for package and assemblies versioning (zlocal by default)

.PARAMETER BuildNumber
Build number to use for package and assemblies versioning (auto-generated if not provided)

.PARAMETER SkipCore
Skips running NuGet.Core.Tests and NuGet.Core.FuncTests

.PARAMETER SkipVS14
Skips running NuGet.Clients.Tests and NuGet.Clients.FuncTests with VS14 toolset

.PARAMETER SkipVS15
Skips running NuGet.Clients.Tests and NuGet.Clients.FuncTests with VS15 toolset

.PARAMETER CI
Indicates the build script is invoked from CI

.NOTES
Some of the functional tests including NuGet server ecosystem will fail outside of Microsoft corpnet.

.EXAMPLE
.\runTests.ps1 -Verbose
Running full test suite

.EXAMPLE
.\runTests.ps1 -sut
Running functional tests only

.EXAMPLE
.\runTests.ps1 -sft -s14 -s15
Running core unit tests only
#>
[CmdletBinding()]
param (
    [ValidateSet("debug", "release")]
    [Alias('c')]
    [string]$Configuration,
    [ValidateSet("release","rtm", "rc", "rc1", "rc2", "rc3", "rc4", "beta", "beta1", "beta2", "final", "preview1", "preview2", "preview3", "xprivate", "zlocal")]
    [Alias('l')]
    [string]$ReleaseLabel = 'zlocal',
    [Alias('n')]
    [int]$BuildNumber,
    [Alias('sb')]
    [switch]$SkipBuild,
    [Alias('sc')]
    [switch]$SkipCore,
    [Alias('s14')]
    [switch]$SkipVS14,
    [Alias('s15')]
    [switch]$SkipVS15,
    [switch]$CI
)

. "$PSScriptRoot\build\common.ps1"

if (-not $Configuration) {
    $Configuration = switch ($CI.IsPresent) {
        $True   { 'Release' } # CI build is Release by default
        $False  { 'Debug' } # Local builds are Debug by default
    }
}

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}

Invoke-BuildStep 'Installing .NET CLI for tests' {
        Install-DotnetCLI -Force:$Force
    } -ev +BuildErrors

Trace-Log "Test suite run #$BuildNumber started at $startTime"

Test-BuildEnvironment -CI:$CI

# Adjust version skipping if only one version installed - if VS15 is not installed, no need to specify SkipVS15
if (-not $SkipVS14 -and -not $VS14Installed) {
    Warning-Log "VS14 build is requested but it appears not to be installed."
    $SkipVS14 = $True
}

if (-not $SkipVS15 -and -not $VS15Installed) {
    Warning-Log "VS15 build is requested but it appears not to be installed."
    $SkipVS15 = $True
}

$BuildErrors = @()

Invoke-BuildStep 'Cleaning package cache' {
        Clear-PackageCache
    } `
    -skip:(-not $CI) `
    -ev +BuildErrors

Invoke-BuildStep 'Running /t:RestoreVS15' {

    & $MSBuildExe build\build.proj /t:RestoreVS15 /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1

    if (-not $?)
    {
        Write-Error "Restore failed!"
        exit 1
    }
} `
-ev +BuildErrors



Invoke-BuildStep 'Running /t:CoreFuncTests' {

    & $MSBuildExe build\build.proj /t:CoreFuncTests /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1

    if (-not $?)
    {
        Write-Error "CoreFuncTests failed!"
        exit 1
    }
} `
-ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' {
        Clear-PackageCache
    } `
    -skip:(-not $CI) `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Test suite run has completed at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}


Write-Host ("`r`n" * 3)
