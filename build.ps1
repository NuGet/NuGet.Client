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

.PARAMETER PackageEndToEnd
Indicates whether to create the end to end package.

.PARAMETER SkipDelaySigning
Indicates whether to skip delay signing.  By default assemblies will be delay signed.

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
    [switch]$PackageEndToEnd,
    [switch]$SkipDelaySigning,
    [switch]$Binlog,
    [switch]$IncludeApex
)

. "$PSScriptRoot\build\common.ps1"

If (-Not $SkipDelaySigning)
{
    & "$PSScriptRoot\scripts\utils\DisableStrongNameVerification.ps1" -skipNoOpMessage
}

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
    exit 1
}

$BuildErrors = @()

Invoke-BuildStep 'Cleaning artifacts' {
    Clear-Artifacts
    Clear-Nupkgs
} `
-skip:$Fast `
-ev +BuildErrors

if($SkipUnitTest){
    $VSTarget = "BuildVS;Pack";
    $VSMessage = "Running Build"
}
else {
    $VSTarget = "RunVS";
    $VSMessage = "Running Build, Pack, Core unit tests, and Unit tests";
}

Invoke-BuildStep 'Running Restore' {

    # Restore
    $args = "build\build.proj", "/t:EnsurePackageReferenceVersionsInSolution", "/p:Configuration=$Configuration"
    if ($Binlog)
    {
        $args += "-bl:msbuild.ensurepr.binlog"
    }

    Trace-Log ". `"$MSBuildExe`" $args"
    & $MSBuildExe @args

    $args = "build\build.proj", "/t:RestoreVS", "/p:Configuration=$Configuration", "/p:ReleaseLabel=$ReleaseLabel", "/p:BuildNumber=$BuildNumber", "/p:IncludeApex=$IncludeApex", "/v:m", "/m:1"
    if ($Binlog)
    {
        $args += "-bl:msbuild.restore.binlog"
    }
    Trace-Log ". `"$MSBuildExe`" $args"
    & $MSBuildExe @args

    if (-not $?)
    {
        Write-Error "Failed - Running Restore"
        exit 1
    }
} `
-ev +BuildErrors


Invoke-BuildStep $VSMessage {

    $args = 'build\build.proj', "/t:$VSTarget", "/p:Configuration=$Configuration", "/p:ReleaseLabel=$ReleaseLabel", "/p:BuildNumber=$BuildNumber", "/p:IncludeApex=$IncludeApex", '/v:m', '/m:1'

    If ($SkipDelaySigning)
    {
        $args += "/p:MS_PFX_PATH="
        $args += "/p:NUGET_PFX_PATH="
    }

    if ($Binlog)
    {
        $args += "-bl:msbuild.build.binlog"
    }

    # Build and (If not $SkipUnitTest) Pack, Core unit tests, and Unit tests for VS
    Trace-Log ". `"$MSBuildExe`" $args"
    & $MSBuildExe @args

    if (-not $?)
    {
        Write-Error "Failed - $VSMessage"
        exit 1
    }
} `
-ev +BuildErrors

Invoke-BuildStep 'Publishing the EndToEnd test package' {
        param($Configuration)
        $EndToEndScript = Join-Path $PSScriptRoot scripts\cibuild\CreateEndToEndTestPackage.ps1 -Resolve
        $OutDir = Join-Path $Artifacts VS15
        & $EndToEndScript -c $Configuration -tv 16 -out $OutDir
    } `
    -args $Configuration `
    -skip:(-not $PackageEndToEnd) `
    -ev +BuildErrors


Invoke-BuildStep 'Running Restore RTM' {

    # Restore for VS
    $args = "build\build.proj", "/t:RestoreVS", "/p:Configuration=$Configuration", "/p:BuildRTM=true", "/p:ReleaseLabel=$ReleaseLabel", "/p:BuildNumber=$BuildNumber", "/p:ExcludeTestProjects=true", "/v:m", "/m:1"

    if ($Binlog)
    {
        $args += "-bl:msbuild.restore.binlog"
    }

    Trace-Log ". `"$MSBuildExe`" $args"
    & $MSBuildExe @args

    if (-not $?)
    {
        Write-Error "Restore failed!"
        exit 1
    }
} `
-skip:(-not $CI)`
-ev +BuildErrors


Invoke-BuildStep 'Packing RTM' {

    # Build and (If not $SkipUnitTest) Pack, Core unit tests, and Unit tests for VS
    $args = "build\build.proj", "/t:BuildVS`;Pack", "/p:Configuration=$Configuration", "/p:BuildRTM=true", "/p:ReleaseLabel=$ReleaseLabel", "/p:BuildNumber=$BuildNumber", "/p:ExcludeTestProjects=true", "/v:m", "/m:1"
    if ($Binlog)
    {
        $args += "-bl:msbuild.pack.binlog"
    }

    Trace-Log ". `"$MSBuildExe`" $args"
    & $MSBuildExe @args

    if (-not $?)
    {
        Write-Error "Packing RTM build failed!"
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