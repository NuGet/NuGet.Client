<#
.SYNOPSIS
Selects the first active VS branch matching the target major.minor version in build\config.props.

.DESCRIPTION
Checks main, rel/insiders, then rel/stable, using their internal bootstrapper channels.
If none matches, the VS integration test pipeline skips building NuGet and running DartLab.
Evaluation and REST failures fail the step rather than being treated as an unsupported version.
#>

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\..\..\build\common.ps1"

$msbuild = Get-MSBuildExe
$propertiesJson = & $msbuild "$NuGetClientRoot\build\config.props" -nologo -getProperty:VsTargetMajorVersion,VsTargetMinorVersion
if ($LASTEXITCODE -ne 0) {
    throw "Failed to evaluate the target Visual Studio version in build\config.props."
}

$properties = ($propertiesJson | Out-String | ConvertFrom-Json).Properties
$targetVersion = [version]"$($properties.VsTargetMajorVersion).$($properties.VsTargetMinorVersion)"
Write-Host "Target Visual Studio version: $targetVersion"

if ([string]::IsNullOrWhiteSpace($env:SYSTEM_ACCESSTOKEN)) {
    throw "SYSTEM_ACCESSTOKEN is required to read the VS repository."
}
if ([string]::IsNullOrWhiteSpace($env:SYSTEM_COLLECTIONURI) -or
    [string]::IsNullOrWhiteSpace($env:SYSTEM_TEAMPROJECT) -or
    [string]::IsNullOrWhiteSpace($env:VS_REPOSITORY_ID)) {
    throw "The collection URI, project name, and VS repository resource ID are required to read the VS repository."
}

$headers = @{
    Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN"
    Accept = 'application/json'
}
$collectionUri = $env:SYSTEM_COLLECTIONURI.TrimEnd('/')
$project = [uri]::EscapeDataString($env:SYSTEM_TEAMPROJECT)
$repositoryUrl = "$collectionUri/$project/_apis/git/repositories/$env:VS_REPOSITORY_ID"
$branches = @(
    @{ Branch = 'main'; Channel = 'int.main' }
    @{ Branch = 'rel/insiders'; Channel = 'int.insiders' }
    @{ Branch = 'rel/stable'; Channel = 'int.stable' }
)

foreach ($candidate in $branches) {
    $branch = [uri]::EscapeDataString($candidate.Branch)
    $uri = "$repositoryUrl/items?path=/version.json&versionDescriptor.version=$branch&versionDescriptor.versionType=branch&includeContent=true&api-version=7.1"
    $item = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
    $versionInfo = $item.content | ConvertFrom-Json
    $versionMatch = [regex]::Match($versionInfo.version, '^(\d+)\.(\d+)(?:[.\-+]|$)')
    if (-not $versionMatch.Success) {
        throw "Invalid version '$($versionInfo.version)' in VS $($candidate.Branch)/version.json."
    }

    $version = [version]"$($versionMatch.Groups[1].Value).$($versionMatch.Groups[2].Value)"
    Write-Host "VS $($candidate.Branch): $version"
    if ($version -eq $targetVersion) {
        Write-Host "Selected VS $($candidate.Branch), channel $($candidate.Channel)."
        Write-Host "##vso[task.setvariable variable=VsBootstrapperBranch]$($candidate.Branch)"
        Write-Host "##vso[task.setvariable variable=VsTargetChannelForTests]$($candidate.Channel)"
        Write-Host "##vso[task.setvariable variable=VsTargetMajorVersion]$($targetVersion.Major)"
        Write-Host "##vso[task.setvariable variable=RunVSIntegrationTests]true"
        Write-Host "##vso[task.setvariable variable=RunVSIntegrationTests;isOutput=true]true"
        return
    }
}

Write-Host "No active VS branch matches $targetVersion. Skipping the NuGet build and VS integration tests."
Write-Host "##vso[task.setvariable variable=RunVSIntegrationTests]false"
Write-Host "##vso[task.setvariable variable=RunVSIntegrationTests;isOutput=true]false"
