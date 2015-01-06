param (
    [switch]$Push, 
    [ValidateSet("debug", "release")][string]$Configuration="debug", 
    [switch]$SkipTests, 
    [string]$PFXPath,
    [switch]$Stable,
    [Parameter(Mandatory=$True)][ValidateSet("NuGet.Client.V3", "NuGet.Client.BaseTypes", "NuGet.Client.V3.VisualStudio", "NuGet.Client.VisualStudio")][string]$Id
)

# build
if ($SkipTests)
{
    $env:DisableRunningUnitTests="true"
}
else
{
    $env:DisableRunningUnitTests="false"
}

if ($PFXPath)
{
    $env:NUGET_PFX_PATH=$PFXPath
}

Write-Host "Building! configuration: $Configuration" -ForegroundColor Cyan
Start-Process "cmd.exe" "/c build.cmd /p:Configuration=$Configuration" -Wait -NoNewWindow
Write-Host "Build complete! configuration: $Configuration" -ForegroundColor Cyan

# assembly containing the release file version to use for the package
$workingDir = (Get-Item -Path ".\" -Verbose).FullName;

# read settings.xml for repo specific settings
[xml]$xml = Get-Content "settings.xml"
$projectPathSetting = Select-Xml "/nupkgs/nupkg[@id='$Id']/csprojPath" $xml | % { $_.Node.'#text' } | Select-Object -first 1
$primaryAssemblySetting = Select-Xml "/nupkgs/nupkg[@id='$Id']/dllName" $xml | % { $_.Node.'#text' } | Select-Object -first 1

# build the csproj and dll full paths
$projectPath = Join-Path $workingDir $projectPathSetting
$projectRoot = Split-Path -parent $projectPath
$primaryAssemblyDir = Join-Path $projectRoot "\bin\$Configuration"
$primaryAssemblyPath = Join-Path $primaryAssemblyDir $primaryAssemblySetting

Write-Host "Project: $projectPath"
Write-Host "Target: $primaryAssemblyPath"

# check signature
Write-Host "Signature check" -ForegroundColor Cyan
$snPath = Join-Path ${env:ProgramFiles(x86)} "Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\sn.exe"
Start-Process $snPath "-Tp $primaryAssemblyPath" -Wait -NoNewWindow

# find the release version from the target assembly
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($primaryAssemblyPath).FileVersion

if (!$version) {
    Write-Error "Unable to find the file version!"
    exit 1
}


$version = $version.TrimEnd('0').TrimEnd('.')

if (!$Stable)
{
    # prerelease labels can have a max length of 20
    $now = [System.DateTime]::UtcNow
    $version += "-" + $now.ToString("ayyyyMMddHHmmss-fff")
}

Write-Host "Package version: $version" -ForegroundColor Cyan

# create the output folder
if ((Test-Path nupkgs) -eq 0) {
    New-Item -ItemType directory -Path nupkgs | Out-Null
}

# Pack
.\nuget.exe pack $projectPath -Properties configuration=$Configuration -symbols -OutputDirectory nupkgs -version $version

# Find the path of the nupkg we just built
$nupkgPath = Get-ChildItem .\nupkgs -filter "*$version.nupkg" | % { $_.FullName }

Write-Host $nupkgPath -ForegroundColor Cyan

if ($Push)
{
    Write-Host "Pushing: $nupkgPath" -ForegroundColor Cyan
    # use nuget.exe setApiKey <key> before running this
    .\.nuget\nuget.exe push $nupkgPath
}
else
{
    Write-Warning "Package not uploaded. Specify -Push to upload this package to nuget.org"
}
