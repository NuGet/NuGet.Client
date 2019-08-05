# Download the CLI install script to Agent.TempDirectory

#Write-Host "Installing dotnet CLI into $Env:AGENT_TEMPDIRECTORY folder for building"

[CmdletBinding(SupportsShouldProcess=$True)]
Param (
    [string]$SDKVersionForBuild
)

# Get version from SDKVersionForBuild, if only branch name specified, use the latest version for this branch
$CliBranch = $SDKVersionForBuild.trim()
$CliChannelAndVersion = $CliBranch -split "\s+"

$Channel = $CliChannelAndVersion[0].trim()
if ($CliChannelAndVersion.count -eq 1) {
    $Version = 'latest'
}else {
    $Version = $CliChannelAndVersion[1].trim()
}

Write-Host "Channel is : $Channel     Version is  : $Version" -ForegroundColor Cyan

$InstallDir = Join-Path $Env:AGENT_TEMPDIRECTORY 'dotnet'

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$DotNetInstall = Join-Path $InstallDir 'dotnet-install.ps1'

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $DotNetInstall


if ([Environment]::Is64BitOperatingSystem) 
{
    $arch = "x64";
}
else 
{
    $arch = "x86";
}

& $DotNetInstall -Channel $Channel -Version $Version -i $InstallDir -Architecture $arch 

$Env:PATH

# Display build info
& dotnet --info