param (
    [Parameter(Mandatory=$true)]
    [string]$Version,
    [string]$NuGetRoot=(Get-Location).Path,
    [string]$NuGetExe=(Join-Path $NuGetRoot ".nuget\nuget.exe"),
    [string]$VisualStudioBuildOutput=(Join-Path $NuGetRoot "artifacts\VisualStudio"),
    [string]$OutputNupkgsFolder=(Join-Path $NuGetRoot "nupkgs"),
    [ValidateSet("Debug", "Release")]
    [string]$Configuration="Debug"
)

Write-Host "NuGet Root is $NuGetRoot"
Write-Host "NuGetExe is $NuGetExe"
Write-Host "VisualStudioBuildOutput is $VisualStudioBuildOutput"
Write-Host "OutputNupkgsFolder is $OutputNupkgsFolder"

trap
{
    Write-Host "PackNuGetVisualStudioPackage.ps1 threw an exception: " $_.Exception -ForegroundColor Red
    exit 1
}

$VisualStudioBuildOutputWithConfiguration = Join-Path $VisualStudioBuildOutput $Configuration
$NuspecPath = Join-Path $VisualStudioBuildOutputWithConfiguration "NuGet.VisualStudio.nuspec"

Write-Host "VisualStudioBuildOutputWithConfiguration is $VisualStudioBuildOutputWithConfiguration"
Write-Host "NuspecPath is $NuspecPath"

$opts = , "pack"
$opts += $NuspecPath
$opts += "-Version", $Version
$opts += "-BasePath", $VisualStudioBuildOutputWithConfiguration
$opts += "-OutputDirectory", $OutputNupkgsFolder

Write-Host "Running 'nuget.exe $opts'"

$p = Start-Process $NuGetExe -wait -NoNewWindow -PassThru -ArgumentList $opts
if($p.ExitCode -ne 0)
{
    throw 'nuget.exe pack failed with exit code ' + $p.ExitCode
}

$NupkgPath = Join-Path $OutputNupkgsFolder ("NuGet.VisualStudio." + $Version + ".nupkg")

if (!(Test-Path $NupkgPath))
{
    throw 'NuGet.VisualStudio package has not been created'
}

Write-Host -ForegroundColor Cyan "NuGet.VisualStudio package has been created successfully"