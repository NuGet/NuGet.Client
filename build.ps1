<#
.SYNOPSIS
Builds NuGet client solutions and creates output artifacts.

.PARAMETER Configuration
Build configuration (debug by default)

.PARAMETER ReleaseLabel
Release label to use for package and assemblies versioning (zlocal by default)

.PARAMETER BuildNumber
Build number to use for package and assemblies versioning (auto-generated if not provided)

.PARAMETER Fast
Runs minimal incremental build. Skips end-to-end packaging step.

.PARAMETER CI
Indicates the build script is invoked from CI

.EXAMPLE
.\build.ps1
To run full clean build, e.g after switching branches

.EXAMPLE
.\build.ps1 -f
Fast incremental build

.EXAMPLE
.\build.ps1 -v -ea Stop
To troubleshoot build issues
#>
[CmdletBinding()]
param (
    [ValidateSet('debug', 'release')]
    [Alias('c')]
    [string]$Configuration,
    [ValidatePattern('^(beta|final|preview|rc|release|rtm|xprivate|zlocal)([0-9]*)$')]
    [Alias('l')]
    [string]$ReleaseLabel = 'zlocal',
    [Alias('n')]
    [int]$BuildNumber,
    [Alias('su')]
    [switch]$SkipUnitTest,
    [Alias('f')]
    [switch]$Fast,
    [switch]$CI,
    [switch]$Rebuild
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
Trace-Log "Build #$BuildNumber started at $startTime"

Test-BuildEnvironment -CI:$CI

if (-not $VSToolsetInstalled) {
    Warning-Log "The build is requested, but no toolset is available"
}

$BuildErrors = @()

Invoke-BuildStep 'Cleaning artifacts' {
    Clear-Artifacts
    Clear-Nupkgs
} `
-skip:$Fast `
-ev +BuildErrors

if($SkipUnitTest){
    $VS15Target = "BuildVS15;Pack";
    $VS15Message = "Running Build for VS 15.0"
}
else {
    $VS15Target = "RunVS15";
    $VS15Message = "Running Build, Pack, Core unit tests, and Unit tests for VS 15.0";
}

Invoke-BuildStep 'Running Restore for VS 15.0' {

    # Restore for VS 15.0
    Trace-Log ". `"$MSBuildExe`" build\build.proj /t:RestoreVS15 /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1"
    & $MSBuildExe build\build.proj /t:RestoreVS15 /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1

    if (-not $?)
    {
        Write-Error "Failed - Running Restore for VS 15.0"
        exit 1
    }
} `
-ev +BuildErrors


Invoke-BuildStep $VS15Message {

    # Build and (If not $SkipUnitTest) Pack, Core unit tests, and Unit tests for VS 15.0
    Trace-Log ". `"$MSBuildExe`" build\build.proj /t:$VS15Target /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1"
    & $MSBuildExe build\build.proj /t:$VS15Target /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1

    if (-not $?)
    {
        Write-Error "Failed - $VS15Message"
        exit 1
    }
} `
-ev +BuildErrors

Invoke-BuildStep 'Publishing the VS15 EndToEnd test package' {
        param($Configuration)
        $EndToEndScript = Join-Path $PSScriptRoot scripts\cibuild\CreateEndToEndTestPackage.ps1 -Resolve
        $OutDir = Join-Path $Artifacts VS15
        & $EndToEndScript -c $Configuration -tv 15 -out $OutDir
    } `
    -args $Configuration `
    -skip:$Fast `
    -ev +BuildErrors


Invoke-BuildStep 'Running Restore for VS 15.0 RTM' {

    # Restore for VS 15.0
    Trace-Log ". `"$MSBuildExe`" build\build.proj /t:RestoreVS15 /p:Configuration=$Configuration /p:BuildRTM=true /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /p:ExcludeTestProjects=true /v:m /m:1 "
    & $MSBuildExe build\build.proj /t:RestoreVS15 /p:Configuration=$Configuration /p:BuildRTM=true /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /p:ExcludeTestProjects=true /v:m /m:1

    if (-not $?)
    {
        Write-Error "Restore failed!"
        exit 1
    }
} `
-skip:(-not $CI)`
-ev +BuildErrors


Invoke-BuildStep 'Packing VS15 RTM' {

    # Build and (If not $SkipUnitTest) Pack, Core unit tests, and Unit tests for VS 15.0
    Trace-Log ". `"$MSBuildExe`" build\build.proj /t:BuildVS15`;Pack /p:Configuration=$Configuration /p:BuildRTM=true /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /p:ExcludeTestProjects=true /v:m /m:1"
    & $MSBuildExe build\build.proj /t:BuildVS15`;Pack /p:Configuration=$Configuration /p:BuildRTM=true  /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /p:ExcludeTestProjects=true /v:m /m:1

    if (-not $?)
    {
        Write-Error "Packing VS15 RTM build failed!"
        exit 1
    }
} `
-skip:(-not $CI)`
-ev +BuildErrors

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}

Write-Host ("`r`n" * 3)