# The following is used to obtain the access token when we will upload the msbuild cache data to a private build cache
Write-Host "Accessing Access Token"
$accessToken = az account get-access-token --resource https://storage.azure.com --query "accessToken" --output tsv
if (-not $accessToken) {
    Write-Error "Unable to get access token"
    exit 1
}

# Set the access token as an environment variable for the plugin - This is the case where we are using the Service Connection access Lifeng's Storage Account
$Env:MSBCACHE_ACCESSTOKEN=$accessToken
$b = $Env:MSBCACHE_ACCESSTOKEN
Write-Host "$($b.Substring(1, 11))"

$CacheEnlistmentRoot = "$PSScriptRoot\..\.."
$MSBuildPath = & "$CacheEnlistmentRoot\scripts\Get-MSBuildPath.ps1" -SixtyFourBit

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

Set-Location -Path "$CacheEnlistmentRoot"
Write-Host "Running $MSBuildPath\msbuild.exe /m:1 /graph /reportfileaccesses /property:Configuration=Debug /property:Platform='Any CPU' `"Nuget.sln`" /v:minimal /bl:`"$Env:Agent_TempDirectory\MSBuildBuildBinLog\build.binlog`""
& $MSBuildPath\msbuild.exe /m:1 /graph /reportfileaccesses /property:Configuration=Debug /property:Platform='Any CPU' `"Nuget.sln`" /v:minimal /bl:`"$Env:Agent_TempDirectory\MSBuildBuildBinLog\build.binlog`"
