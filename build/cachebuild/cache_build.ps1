Write-Host "Copying cache_build.props and cache_build.targets to the ImportBefore and ImportAFter folders respectively"
$LocalAppDataFolder = $Env:LOCALAPPDATA
$ImportAfterFolder = Join-Path $LocalAppDataFolder "Microsoft\MSBuild\Current\Imports\Microsoft.Common.props\ImportAfter"
$ImportBeforeFolder = Join-Path $LocalAppDataFolder "Microsoft\MSBuild\Current\Imports\Microsoft.Common.props\ImportBefore"

New-Item -Path $ImportBeforeFolder -ItemType Directory -Force | Out-Null
New-Item -Path $ImportAfterFolder -ItemType Directory -Force | Out-Null
Copy-Item -Path "$PSScriptRoot\cache_build.props" -Destination $ImportBeforeFolder -Force
Copy-Item -Path "$PSScriptRoot\cache_build.targets" -Destination $ImportAfterFolder -Force

# Set the cache log directory so we tell it where to go
$MSBuildCacheLogDirectory = "$Env:Agent_TempDirectory\MSBuildCacheLogs"
$MSBuildCacheLocalCacheRootPath = "$Env:Agent_TempDirectory\MSBuildCacheLocalRoot"


# Set the location for the cache auth file
$MSBuildCacheBuildCacheConfigurationFile = "c:\buildcacheconfig.json"

Write-Host "##vso[task.setvariable variable=MSBuildCacheLogDirectory]$MSBuildCacheLogDirectory"
Write-Host "##vso[task.setvariable variable=MSBuildCacheLocalCacheRootPath]$MSBuildCacheLocalCacheRootPath"
Write-Host "##vso[task.setvariable variable=MSBuildCacheBuildCacheConfigurationFile]$MSBuildCacheBuildCacheConfigurationFile"
