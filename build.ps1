param (
    [ValidateSet("debug", "release")][string]$Configuration="debug",
    [switch]$SkipTests,
    [switch]$SkipRestore,
    [switch]$Fast,
	[switch]$CleanCache,
	[switch]$PublicRelease
)

###Functions###

function RestoreXProj($file)
{
    $ext = [System.IO.Path]::GetExtension($file.FullName)

    if ($ext -eq ".xproj")
    {
        $xprojDir = [System.IO.Path]::GetDirectoryName($file.FullName)
        $projectJsonFile = [System.IO.Path]::Combine($xprojDir, "project.json")

        Write-Host "Restoring $projectJsonFile"
        Write-Host "dnu restore '$projectJsonFile' -s https://www.myget.org/F/nuget-volatile/api/v2/ -s https://www.nuget.org/api/v2/"
        & dnu restore "$($projectJsonFile)" -s https://www.myget.org/F/nuget-volatile/api/v2/ -s https://www.nuget.org/api/v2/

        if ($LASTEXITCODE -ne 0)
        {
            throw "Restore failed $projectJsonFile"
        }
    }
}

## Clean the machine level cache from all package
function CleanCache()
{
	Write-Host Removing MEF cache

	if (Test-Path $env:localappdata\Microsoft\VisualStudio\14.0Exp)
	{
		rm -r $env:localappdata\Microsoft\VisualStudio\14.0Exp -Force
	}

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
    ## For local build using the following format
    $env:DNX_BUILD_VERSION="local-$timestamp"

    if ($SkipRestore -eq $False)
    {
        Write-Host "Restoring tools"
        & $nugetExe restore .nuget\packages.config -SolutionDirectory .

        Write-Host "Restoring XProj packages"
        foreach ($file in (Get-ChildItem "src" -rec))
        {
            RestoreXProj($file)
        }
    }

    $artifacts = Join-Path $executingScriptDirectory "artifacts"
    $artifactsSrc = Join-Path $artifacts "src"
    $nupkgsDir = Join-Path $executingScriptDirectory "nupkgs"
    $artifactsTest = Join-Path $artifacts "test"

    foreach ($file in (Get-ChildItem "src" -rec))
    {
        $ext = [System.IO.Path]::GetExtension($file.FullName)

        if ($ext -eq ".xproj")
        {
            $srcDir = [System.IO.Path]::GetDirectoryName($file.FullName)
            $outDir = Join-Path $artifacts $file.BaseName

            & dnu pack "$($srcDir)" --out $outDir

            if ($LASTEXITCODE -ne 0)
            {
                throw "Build failed $srcDir"
            }
        }
    }

    if ($SkipTests -eq $False)
    {
        foreach ($file in (Get-ChildItem "test" -rec))
        {
            RestoreXProj($file)
        }

        foreach ($file in (Get-ChildItem "test" -rec))
        {
            $ext = [System.IO.Path]::GetExtension($file.FullName)

            if ($ext -eq ".xproj")
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
    }

    ## Coping nupkgs
    Write-Host "Coping the packages to" $artifactsPackages
    Get-ChildItem *.nupkg -Recurse | % { Move-Item $_ $nupkgsDir }
}

function BuildCSproj()
{
    # Restore packages for NuGet.Tooling sloution
    $nugetExe restore .\NuGet.Tooling.sln

    # Build the sloution
    & msbuild /p:Configuration:$Configuration .\NuGet.Tooling.sln
}

###Functions###

# Move to the script directory
$executingScriptDirectory = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent
pushd $executingScriptDirectory

$nugetExe = ".nuget\nuget.exe"
$dnvmLoc = Join-Path $env:USERPROFILE ".dnx\bin\dnvm.cmd"
$timestamp = [DateTime]::UtcNow.ToString("yyMMddHHmmss");

# Download NuGet.exe if missing
if ((Test-Path $nugetExe) -eq $False)
{
    Write-Host "Downloading nuget.exe"
    wget http://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nugetExe
}

## Validating DNVM installed and install it if missing
if ((Test-Path $dnvmLoc) -eq $False)
{
    Write-Host "Downloading DNVM"
    &{$Branch='dev';iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}
}

## Make sure the needed DNX runtimes ex
Write-Host "Validating the correct DNX runtime set"
$env:DNX_FEED="https://www.nuget.org/api/v2"
& dnvm install 1.0.0-beta7 -runtime CoreCLR -arch x86
& dnvm install 1.0.0-beta7 -runtime CLR -arch x86 -a default

if($CleanCache)
{
	CleanCache
}

## Building all XProj projects
BuildXproj

## Building the Tooling sloution
BuildCSproj

popd