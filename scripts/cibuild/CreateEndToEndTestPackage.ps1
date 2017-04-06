<#
.SYNOPSIS
Creates end-to-end test package for test pass

.PARAMETER Configuration
API.Test build configuration to place in test package. Debug by default.

.PARAMETER ToolsetVersion
Toolset version

.PARAMETER OutputDirectory
Output directory where EndToEnd.zip package file will be created.
Will use current directory if not provided.

.PARAMETER NuGetRoot
Optional. NuGet.Client repository root
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$False)]
    [Alias('c')]
    [string]$Configuration = 'Debug',
    [Parameter(Mandatory=$False)]
    [ValidateSet(14,15)]
    [Alias('tv')]
    [int]$ToolsetVersion = 14,
    [Parameter(Mandatory=$False)]
    [Alias('out')]
    [string]$OutputDirectory = $PWD,
    [Parameter(Mandatory=$False)]
    [string]$NuGetRoot
)

. "$PSScriptRoot\..\common.ps1"

if (-not $NuGetRoot -and (Test-Path Env:\NuGetRoot)) {
    $NuGetRoot = $env:NuGetRoot
}

if (-not $NuGetRoot) {
    $NuGetRoot = Join-Path $PSScriptRoot '..\..\' -Resolve
}

$WorkingDirectory = New-TempDir

$opts = '/s', '/z', '/r:3', '/w:30', '/np', '/nfl'

if ($VerbosePreference) {
    $opts += '/v'
}
else {
    $opts += '/ndl', '/njs'
}

try {
    $TestSource = Join-Path $NuGetRoot test\EndToEnd -Resolve
    Write-Verbose "Copying all test files from '$TestSource' to '$WorkingDirectory'"
    & robocopy $TestSource $WorkingDirectory $opts
    if($lastexitcode -gt 1) { exit 1 }

    $TestExtension = Join-Path $NuGetRoot "artifacts\API.Test\${ToolsetVersion}.0\bin\${Configuration}\net46\API.Test.dll" -Resolve
    Write-Verbose "Copying test extension from '$TestExtension' to '$WorkingDirectory'"
    Copy-Item $TestExtension $WorkingDirectory

    $ScriptsDirectory = Join-Path $WorkingDirectory scripts
    New-Item -ItemType Directory -Force -Path $ScriptsDirectory | Out-Null

    $ScriptsSource = Join-Path $NuGetRoot Scripts\e2etests -Resolve
    Write-Verbose "Copying test scripts from '$ScriptsSource' to '$ScriptsDirectory'"
    & robocopy $ScriptsSource $ScriptsDirectory "*.ps1" $opts
    if($lastexitcode -gt 1) { exit 1 }

    if (-not (Test-Path $OutputDirectory)) {
        md $OutputDirectory | Out-Null
    }

    $TestPackage = Join-Path $OutputDirectory EndToEnd.zip
    Write-Verbose "Creating test package '$TestPackage'"
    Remove-Item $TestPackage -Force -ea Ignore | Out-Null
    New-ZipArchive $WorkingDirectory $TestPackage

    Write-Output "Created end-to-end test package for toolset '${ToolsetVersion}.0' at '$TestPackage'"
}
finally {
    rm $workingDirectory -r -Force -WhatIf:$false
    exit 0
}