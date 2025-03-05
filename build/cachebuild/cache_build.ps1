$CacheEnlistmentRoot = [IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$MSBuildPath = & "$CacheEnlistmentRoot\Scripts\Get-MSBuildPath.ps1" -SixtyFourBit

if (-not $MSBuildPath) {
    Write-Error "Unable to continue without MSBuildPath"
    exit 1
}

Write-Host "Copying cache_build.props and cache_build.targets to the ImportBefore and ImportAFter folders respectively"
$LocalAppDataFolder = $Env:LOCALAPPDATA
$ImportAfterFolder = Join-Path $LocalAppDataFolder "Microsoft\MSBuild\Current\Imports\Microsoft.Common.props\ImportAfter"
$ImportBeforeFolder = Join-Path $LocalAppDataFolder "Microsoft\MSBuild\Current\Imports\Microsoft.Common.props\ImportBefore"

New-Item -Path $ImportBeforeFolder -ItemType Directory -Force | Out-Null
New-Item -Path $ImportAfterFolder -ItemType Directory -Force | Out-Null
Copy-Item -Path "$PSScriptRoot\cache_build.props" -Destination $ImportBeforeFolder -Force
Copy-Item -Path "$PSScriptRoot\cache_build.targets" -Destination $ImportAfterFolder -Force

# Set the cache log directory so we tell it where to go
$Env:MSBuildCacheLogDirectory = "$Env:Agent_TempDirectory\MSBuildCacheLogs"
$Env:MSBuildCacheLocalCacheRootPath = "$Env:Agent_TempDirectory\MSBuildCacheLocalRoot"

# Set the location for the cache auth file
$Env:MSBuildCacheBuildCacheConfigurationFile = 'c:\buildcacheconfig.json'

Write-Host "Running $MSBuildPath\msbuild.exe /m:1 /graph /reportfileaccesses /property:Configuration=Debug /property:Platform='Any CPU' `"$CacheEnlistmentRoot\Nuget.sln`" /v:minimal /bl:`"$Env:Agent_TempDirectory\MSBuildBuildBinLog\build.binlog`""
& $MSBuildPath\msbuild.exe /m:1 /graph /reportfileaccesses /property:Configuration=Debug /property:Platform='Any CPU' `"$CacheEnlistmentRoot\Nuget.sln`" /v:minimal /bl:`"$Env:Agent_TempDirectory\MSBuildBuildBinLog\build.binlog`"
