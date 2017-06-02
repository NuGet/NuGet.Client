<#
.SYNOPSIS
Builds NuGet client solutions and creates output artifacts.

.PARAMETER Configuration
Build configuration (debug by default)

.PARAMETER ReleaseLabel
Release label to use for package and assemblies versioning (zlocal by default)

.PARAMETER BuildNumber
Build number to use for package and assemblies versioning (auto-generated if not provided)

.PARAMETER MSPFXPath
Path to a code signing certificate for delay-sigining (optional)

.PARAMETER NuGetPFXPath
Path to a code signing certificate for delay-sigining (optional)

.PARAMETER SkipVS14
Skips building binaries targeting Visual Studio "14" (released as Visual Studio 2015)

.PARAMETER SkipVS15
Skips building binaries targeting Visual Studio "15"

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
.\build.ps1 -s14 -s15
To build core projects only

.EXAMPLE
.\build.ps1 -v -ea Stop
To troubleshoot build issues
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
    [Alias('mspfx')]
    [string]$MSPFXPath,
    [Alias('nugetpfx')]
    [string]$NuGetPFXPath,
    [Alias('s14')]
    [switch]$SkipVS14,
    [Alias('s15')]
    [switch]$SkipVS15,
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

Invoke-BuildStep 'Cleaning artifacts' {
    Clear-Artifacts
    Clear-Nupkgs
} `
-skip:$Fast `
-ev +BuildErrors

Invoke-BuildStep 'Set delay signing options' {
    Set-DelaySigning $MSPFXPath $NuGetPFXPath
} `
-ev +BuildErrors

if($SkipUnitTest){
    $VS14Target = "BuildVS14";
    $VS14Message = "Running Build for VS 14.0";
    $VS15Target = "BuildVS15;Pack";
    $VS15Message = "Running Build for VS 15.0"
}
else {
    $VS14Target = "RunVS14";
    $VS14Message = "Running Build and Unit tests for VS 14.0";
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
-skip:$SkipVS15 `
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
-skip:$SkipVS15 `
-ev +BuildErrors

# building the vs15 nuget.tools.vsix for vs insertion
# This step relies on the earlier restore for VS15
Invoke-BuildStep 'Building nuget.tools.vsix for Insertion' {

    # Build and (If not $SkipUnitTest) Pack, Core unit tests, and Unit tests for VS 15.0
    Trace-Log ". `"$MSBuildExe`" build\build.proj /t:BuildVS15 /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /p:ExcludeTestProjects=true /p:IsInsertable=true /v:m /m:1"
    & $MSBuildExe build\build.proj /t:BuildVS15 /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /p:ExcludeTestProjects=true /p:IsInsertable=true /v:m /m:1 

    if (-not $?)
    {
        Write-Error "Packing VS15 RTM build failed!"
        exit 1
    }
} `
-skip:($SkipVS15 -or (-not $CI))`
-ev +BuildErrors


Invoke-BuildStep 'Running Restore for VS 14.0' {

    # Restore for VS 14.0
    Trace-Log ". `"$MSBuildExe`" build\build.proj /t:RestoreVS14 /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1"
    & $MSBuildExe build\build.proj /t:RestoreVS14 /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1

    if (-not $?)
    {
        Write-Error "Failed - Running Restore for VS 14.0"
        exit 1
    }
} `
-skip:$SkipVS14 `
-ev +BuildErrors


Invoke-BuildStep $VS14Message {

    # Build and (If not $SkipUnitTest) Run Unit tests for VS 14.0
    Trace-Log ". `"$MSBuildExe`" build\build.proj /t:$VS14Target /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1"
    & $MSBuildExe build\build.proj /t:$VS14Target /p:Configuration=$Configuration /p:ReleaseLabel=$ReleaseLabel /p:BuildNumber=$BuildNumber /v:m /m:1

    if (-not $?)
    {
        Write-Error "Failed - $VS14Message"
        exit 1
    }
} `
-skip:$SkipVS14 `
-ev +BuildErrors

Invoke-BuildStep 'Publishing the VS14 EndToEnd test package' {
        param($Configuration)
        $EndToEndScript = Join-Path $PSScriptRoot scripts\cibuild\CreateEndToEndTestPackage.ps1 -Resolve
        $OutDir = Join-Path $Artifacts VS14
        & $EndToEndScript -c $Configuration -tv 14 -out $OutDir
    } `
    -args $Configuration `
    -skip:($Fast -or $SkipVS14) `
    -ev +BuildErrors

Invoke-BuildStep 'Publishing the VS15 EndToEnd test package' {
        param($Configuration)
        $EndToEndScript = Join-Path $PSScriptRoot scripts\cibuild\CreateEndToEndTestPackage.ps1 -Resolve
        $OutDir = Join-Path $Artifacts VS15
        & $EndToEndScript -c $Configuration -tv 15 -out $OutDir
    } `
    -args $Configuration `
    -skip:($Fast -or $SkipVS15) `
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
-skip:($SkipVS15 -or (-not $CI))`
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
-skip:($SkipVS15 -or (-not $CI))`
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