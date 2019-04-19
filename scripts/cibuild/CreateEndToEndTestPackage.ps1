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
    [ValidateSet(15,16)]
    [Alias('tv')]
    [int]$ToolsetVersion = 15,
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

    # Copy everything except the /Packages directory.
    # Instead, the /Packages directory will be copied from the NuGet.Client.EndToEnd.TestData package.
    $e2eOpts = $opts + @('/XD', 'Packages')
    & robocopy $TestSource $WorkingDirectory $e2eOpts
    if ($lastexitcode -gt 1) {
        exit 1
    }

    $TestSource = Join-Path $NuGetRoot packages\NuGet.Client.EndToEnd.TestData.1.0.0\content\Packages -Resolve
    $packagesDirectory = Join-Path $WorkingDirectory 'Packages'
    Write-Verbose "Copying all test data from '$TestSource' to '$packagesDirectory'"
    & robocopy $TestSource $packagesDirectory $opts

    $TestExtensionDirectoryPath = Join-Path $NuGetRoot "artifacts\API.Test\${ToolsetVersion}.0\bin\${Configuration}\net472"
    Write-Verbose "Copying test extension from '$TestExtensionDirectoryPath' to '$WorkingDirectory'"
    & robocopy $TestExtensionDirectoryPath $WorkingDirectory API.Test.* $opts

    $GeneratePackagesUtil = Join-Path $NuGetRoot "artifacts\GenerateTestPackages\${ToolsetVersion}.0\bin\${Configuration}\net472"
    Write-Verbose "Copying utility binaries from `"$GeneratePackagesUtil`" to `"$WorkingDirectory`""
    & robocopy $GeneratePackagesUtil $WorkingDirectory *.exe *.dll *.pdb $opts

    $ScriptsDirectory = Join-Path $WorkingDirectory scripts
    New-Item -ItemType Directory -Force -Path $ScriptsDirectory | Out-Null

    $ScriptsSource = Join-Path $NuGetRoot Scripts\e2etests -Resolve
    Write-Verbose "Copying test scripts from '$ScriptsSource' to '$ScriptsDirectory'"
    & robocopy $ScriptsSource $ScriptsDirectory '*.ps1' $opts

    if ($lastexitcode -gt 1) {
        exit 1
    }

    if (-not (Test-Path $OutputDirectory)) {
        mkdir $OutputDirectory | Out-Null
    }

    $TestPackage = Join-Path $OutputDirectory EndToEnd.zip
    Write-Verbose "Creating test package '$TestPackage'"
    Remove-Item $TestPackage -Force -ea Ignore | Out-Null
    New-ZipArchive $WorkingDirectory $TestPackage

    Write-Output "Created end-to-end test package for toolset '${ToolsetVersion}.0' at '$TestPackage'"
}
finally {
    Remove-Item $workingDirectory -r -Force -WhatIf:$false
    exit 0
}