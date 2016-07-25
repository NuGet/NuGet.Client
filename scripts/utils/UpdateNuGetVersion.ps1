<#
.SYNOPSIS
Replaces all occurances of build version string in project.json and other files

.PARAMETER NewVersion
New version string to set

.PARAMETER OldVersion
Optional. Old version string to replace.
Will use version from .teamcity.properties file if not specified.

.PARAMETER NuGetRoot
Optional. NuGet client repository root.

.PARAMETER Force
Optional switch to force replacing text when new and old versions are the same
#>
[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [Parameter(Mandatory=$true, Position=0)]
    [Alias('version')]
    [string]$NewVersion,
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

if (-not $OldVersion -and (Test-Path "$NuGetRoot\.teamcity.properties")) {
    $properties = ReadPropertiesFile "$NuGetRoot\.teamcity.properties"
    $OldVersion = $properties['ReleaseProductVersion']
}

if (-not $OldVersion) {
    throw "OLD version string can't be found"
}

if ($OldVersion -eq $NewVersion -and -not $Force) {
    Write-Output "NO-OP [$OldVersion == $NewVersion]"
    exit 0
}

Write-Output "Updating NuGet version [$OldVersion => $NewVersion]"

gci -r project.json | %{ $_.FullName } | ReplaceTextInFiles -old $OldVersion -new $NewVersion

$miscFiles = @(
    "src\NuGet.Clients\VsExtension\source.extension.dev14.vsixmanifest",
    "src\NuGet.Clients\VsExtension\source.extension.dev15.vsixmanifest",
    "src\NuGet.Clients\VsExtension\NuGetPackage.cs",
    "build\common.props",
    ".teamcity.properties",
    "appveyor.yml"
)

$miscFiles | %{ Join-Path $NuGetRoot $_ -Resolve } | ReplaceTextInFiles -old $OldVersion -new $NewVersion