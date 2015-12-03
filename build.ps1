param (
    [ValidateSet("debug", "release")][string]$Configuration="debug",
    [ValidateSet("Release","rtm", "rc", "beta", "local")][string]$ReleaseLabel="local",
    [string]$BuildNumber,
    [switch]$SkipTests,
    [switch]$SkipRestore,
	[switch]$CleanCache,
	[switch]$SkipILMerge,
	[switch]$DelaySign,
    [string]$MSPFXPath,
    [string]$NuGetPFXPath,
    [switch]$SkipXProj,
    [switch]$SkipSubModules
)

###Functions###

function RestoreXProj($file)
{
    $xprojDir = [System.IO.Path]::GetDirectoryName($file.FullName)
    $projectJsonFile = [System.IO.Path]::Combine($xprojDir, "project.json")

    Write-Host "Restoring $projectJsonFile"
    Write-Host "dnu restore '$projectJsonFile' -s https://www.myget.org/F/nuget-volatile/api/v3/index.json -s https://api.nuget.org/v3/index.json"
    & dnu restore "$($projectJsonFile)" -s https://www.myget.org/F/nuget-volatile/api/v3/index.json -s https://api.nuget.org/v3/index.json

    if ($LASTEXITCODE -ne 0)
    {
        throw "Restore failed $projectJsonFile"
    }
}

## Clean the machine level cache from all package
function CleanCache()
{
	Write-Host Removing DNX packages

	if (Test-Path $env:userprofile\.dnx\packages)
	{
		rm -r $env:userprofile\.dnx\packages -Force
	}

	Write-Host Removing .NUGET packages

	if (Test-Path $env:userprofile\.nuget\packages)
	{
		rm -r $env:userprofile\.nuget\packages -Force
	}

	Write-Host Removing DNU cache

	if (Test-Path $env:localappdata\dnu\cache)
	{
		rm -r $env:localappdata\dnu\cache -Force
	}

	Write-Host Removing NuGet web cache

	if (Test-Path $env:localappdata\NuGet\v3-cache)
	{
		rm -r $env:localappdata\NuGet\v3-cache -Force
	}

	Write-Host Removing NuGet machine cache

	if (Test-Path $env:localappdata\NuGet\Cache)
	{
		rm -r $env:localappdata\NuGet\Cache -Force
	}
}

## Building XProj projects
function BuildXproj()
{
    ## Setting the DNX build version
    if($ReleaseLabel -ne "Release")
    {
        $env:DNX_BUILD_VERSION="$ReleaseLabel-$BuildNumber"
    }

    # Setting the DNX AssemblyFileVersion
    $env:DNX_ASSEMBLY_FILE_VERSION=$BuildNumber

    if ($SkipRestore -eq $False)
    {
        Write-Host "Restoring XProj packages"
        foreach ($file in (Get-ChildItem "src" -rec -Filter "*.xproj"))
        {
            RestoreXProj($file)
        }
    }

    $artifactsSrc = Join-Path $artifacts "src\NuGet.Core"
    $artifactsTest = Join-Path $artifacts "test"

    foreach ($file in (Get-ChildItem "src" -rec -Filter "*.xproj"))
    {
        $srcDir = [System.IO.Path]::GetDirectoryName($file.FullName)
        $outDir = Join-Path $artifacts $file.BaseName

        & dnu pack "$($srcDir)" --configuration $Configuration --out $outDir

        if ($LASTEXITCODE -ne 0)
        {
            throw "Build failed $srcDir"
        }
    }

    if ($SkipTests -eq $False)
    {
        # Test assemblies should not be signed
        if (Test-Path Env:\DNX_BUILD_KEY_FILE)
        {
            Remove-Item Env:\DNX_BUILD_KEY_FILE
        }

        if (Test-Path Env:\DNX_BUILD_DELAY_SIGN)
        {
            Remove-Item Env:\DNX_BUILD_DELAY_SIGN
        }

        foreach ($file in (Get-ChildItem "test\NuGet.Core.Tests" -rec -filter "*.xproj"))
        {
            RestoreXProj($file)
        }

        foreach ($file in (Get-ChildItem "test\NuGet.Core.Tests" -rec -Filter "*.xproj"))
        {
            $srcDir = [System.IO.Path]::GetDirectoryName($file.FullName)
            Write-Host "Running tests in $srcDir"

            pushd $srcDir
            & dnx test
            popd

            if ($LASTEXITCODE -ne 0)
            {
                throw "Tests failed $srcDir"
            }
        }
    }

    ## Copying nupkgs
    Write-Host "Copying the packages to" $artifactsPackages
    Get-ChildItem $artifacts\*.nupkg -Recurse | % { Move-Item $_ $nupkgsDir }
}

function BuildCSproj()
{
    #Building the microsoft interop package for the test.utility
    $interopLib = ".\lib\Microsoft.VisualStudio.ProjectSystem.Interop"
    & dnu restore $interopLib -s https://www.myget.org/F/nuget-volatile/api/v2/ -s https://www.nuget.org/api/v2/
    & dnu pack $interopLib
    Get-ChildItem $interopLib\*.nupkg -Recurse | % { Move-Item $_ $nupkgsDir }

    # Restore packages for NuGet.Tooling solution
    & $nugetExe restore -msbuildVersion 14 .\NuGet.Clients.sln

    # Build the solution
    & $msbuildExe .\NuGet.Clients.sln "/p:Configuration=$Configuration;ReleaseLabel=$ReleaseLabel;BuildNumber=$BuildNumber;RunTests=!$SkipTests"

    if ($LASTEXITCODE -ne 0)
    {
        throw "NuGet.Clients.sln Build failed "
    }

    Write-Host "Copying the Vsix to $artifacts"
    $visxLocation = Join-Path $artifacts "$Configuration\NuGet.Clients\VsExtension"
    Copy-Item $visxLocation\NuGet.Tools.vsix $artifacts
}

function ILMergeNuGet()
{
    $nugetArtifictFolder = Join-Path $artifacts "$Configuration\NuGet.Clients\NuGet.CommandLine"

    pushd $nugetArtifictFolder

    Write-Output "Creating the ilmerged nuget.exe"
    & $ILMerge NuGet.exe NuGet.Client.dll NuGet.Commands.dll NuGet.Configuration.dll NuGet.ContentModel.dll NuGet.Core.dll NuGet.Credentials.dll NuGet.DependencyResolver.Core.dll NuGet.DependencyResolver.dll NuGet.Frameworks.dll NuGet.LibraryModel.dll NuGet.Logging.dll NuGet.PackageManagement.dll NuGet.Packaging.Core.dll NuGet.Packaging.Core.Types.dll NuGet.Packaging.dll NuGet.ProjectManagement.dll NuGet.ProjectModel.dll NuGet.Protocol.Core.Types.dll NuGet.Protocol.Core.v2.dll NuGet.Protocol.Core.v3.dll NuGet.Repositories.dll NuGet.Resolver.dll NuGet.RuntimeModel.dll NuGet.Versioning.dll Microsoft.Web.XmlTransform.dll Newtonsoft.Json.dll /log:mergelog.txt /out:$artifacts\NuGet.exe

    if ($LASTEXITCODE -ne 0)
    {
        throw "ILMerge failed"
    }

    popd
}

###Functions###

# Move to the script directory
$executingScriptDirectory = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent
pushd $executingScriptDirectory

$msbuildExe = "${env:ProgramFiles(x86)}\MSBuild\14.0\Bin\msbuild.exe"
$nugetExe = ".nuget\nuget.exe"
$ILMerge = Join-Path $executingScriptDirectory "packages\ILMerge.2.14.1208\tools\ILMerge.exe"
$dnvmLoc = Join-Path $env:USERPROFILE ".dnx\bin\dnvm.cmd"
$nupkgsDir = Join-Path $executingScriptDirectory "nupkgs"
$artifacts = Join-Path $executingScriptDirectory "artifacts"
$startTime = [DateTime]::UtcNow

Write-Host "Build started at " $startTime
Write-Host

if ($SkipSubModules -eq $False)
{
    if ((Test-Path -Path "submodules/FileSystem/src") -eq $False)
    {
        Write-Host "Updating and initializing submodules"
        & git submodule update --init
    }
    else
    {
        Write-Host "Updating submodules"
        & git submodule update
    }
}

# Download NuGet.exe if missing
if ((Test-Path $nugetExe) -eq $False)
{
    Write-Host "Downloading nuget.exe"
    wget https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe -OutFile $nugetExe
}

# Restoring tools required for build
if ($SkipRestore -eq $False)
{
    Write-Host "Restoring tools"
    & $nugetExe restore .nuget\packages.config -SolutionDirectory .
}

## Validating DNVM installed and install it if missing
if ((Test-Path $dnvmLoc) -eq $False)
{
    Write-Host "Downloading DNVM"
    &{$Branch='dev';iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}
}

## Clean artifacts and nupkgs folder
if (Test-Path $nupkgsDir)
{
    Write-Host "Cleaning nupkgs folder"
    Remove-Item $nupkgsDir\*.nupkg
}

if( Test-Path $artifacts)
{
    Write-Host "Cleaning the artifacts folder"
    Remove-Item $artifacts\*.* -Recurse
}

## Make sure the needed DNX runtimes ex
Write-Host "Validating the correct DNX runtime set"
$env:DNX_FEED="https://www.nuget.org/api/v2"
& dnvm install 1.0.0-rc1-update1 -runtime CoreCLR -arch x86
& dnvm install 1.0.0-rc1-update1 -runtime CLR -arch x86 -alias default

if($CleanCache)
{
	CleanCache
}

# enable delay signed build
if ($DelaySign)
{
    if (Test-Path $MSPFXPath)
    {
        Write-Host "Setting NuGet.Core solution to delay sign using $MSPFXPath"
        $env:DNX_BUILD_KEY_FILE=$MSPFXPath
        $env:DNX_BUILD_DELAY_SIGN=$true
    }

    if (Test-Path $NuGetPFXPath)
    {
        Write-Host "Setting NuGet.Clients solution to delay sign using $NuGetPFXPath"
        $env:NUGET_PFX_PATH= $NuGetPFXPath

        Write-Host "Using the Microsoft Key for NuGet Command line $MSPFXPath"
        $env:MS_PFX_PATH=$MSPFXPath
    }
}

$SemanticVersionDate = "2015-10-8"

if(!$BuildNumber)
{
    $R = ""
    $BuildNumber = ([Math]::DivRem(([System.DateTime]::Now.Subtract([System.DateTime]::Parse($SemanticVersionDate)).TotalMinutes), 5, [ref]$R)).ToString('F0')
}
else
{
    $buildNum = [int]$BuildNumber
    $BuildNumber = $buildNum.ToString("D4");
}

if(!$SkipXProj)
{
    ## Building all XProj projects
    BuildXproj
}

## Building the Tooling solution
BuildCSproj

if ($SkipILMerge -eq $False)
{
    ## Merging the NuGet.exe
    ILMergeNuGet
}

## Calculating Build time
$endTime = [DateTime]::UtcNow
$diff = [math]::Round(($endTime - $startTime).TotalMinutes, 4)

Write-Host
Write-Host "Build ended at " $endTime
Write-Host "Build took " $diff "(mins)"
Write-Host

popd