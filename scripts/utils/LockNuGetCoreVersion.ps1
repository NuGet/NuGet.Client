<#
.SYNOPSIS
Replaces floating version of NuGet core dependencies in NuGet clients projects to "{semver}-{label}-*".
Mostly needed for release branches when dev builds in myget feed have more advanced version.

.PARAMETER LockVersion
Optional. New version of NuGet core packages.
Defaults to current "{semver}-{labe}-*" from .teamcity.properties

.PARAMETER OldVersion
Optional. Old version string to look for when scanning project.json's.
Will use default version "{semver}-*" from .teamcity.properties file if not specified.

.PARAMETER NuGetRoot
Optional. NuGet client repository root.

.PARAMETER Force
Optional switch to force replacing text when new and old versions are the same
#>
[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [Parameter(Mandatory=$false, Position=0)]
    [Alias('version')]
    [string]$LockVersion,
    [Parameter(Mandatory=$false, Position=1)]
    [string]$OldVersion,
    [Parameter(Mandatory=$false, Position=2)]
    [string]$NuGetRoot,
    [switch]$Force)

. "$PSScriptRoot\..\common.ps1"

if (-not $NuGetRoot -and (Test-Path Env:\NuGetRoot)) {
    $NuGetRoot = $env:NuGetRoot
}

if (-not $NuGetRoot) {
    $NuGetRoot = Join-Path $PSScriptRoot '..\..\' -Resolve
}

if (Test-Path "$NuGetRoot\.teamcity.properties") {
    $properties = ReadPropertiesFile "$NuGetRoot\.teamcity.properties"
}

if (-not $LockVersion -and $properties) {
    $LockVersion = "$($properties['ReleaseProductVersion'])-$($properties['ReleaseLabel'])-*"
}

if (-not $LockVersion) {
    throw "LOCK version string can't be found"
}

if (-not $OldVersion -and $properties) {
    $OldVersion = "$($properties['ReleaseProductVersion'])-*"
}

if (-not $OldVersion) {
    throw "OLD version string can't be found"
}

if ($OldVersion -eq $LockVersion -and -not $Force) {
    Write-Output "NO-OP [$OldVersion == $LockVersion]"
    exit 0
}

Write-Output "Locking NuGet core projects version [$OldVersion => $LockVersion]"

gci "$NuGetRoot\src\NuGet.Clients" -include project.json -r | %{ $_.FullName } | ReplaceTextInFiles -old $OldVersion -new $LockVersion
