param (
    [ValidateSet("debug", "release")][string]$Configuration="debug",
    [switch]$SkipTests,
    [switch]$SkipRestore,
    [switch]$Fast,
	[switch]$CleanCache,
	[switch]$PublicRelease
)

# Move to the script directory
$executingScriptDirectory = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent
pushd $executingScriptDirectory

$nugetExe = ".nuget\nuget.exe"

if ((Test-Path $nugetExe) -eq $False)
{
    Write-Host "Downloading nuget.exe"
    wget http://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nugetExe
}

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

if ($SkipRestore -eq $False)
{
    Write-Host "Restoring tools"
    & nuget.exe restore .nuget\packages.config -SolutionDirectory .

    Write-Host "Downloading DNX"
    $env:DNX_FEED="https://www.nuget.org/api/v2"
    & dnvm install 1.0.0-beta7 -runtime CoreCLR -arch x86
    & dnvm install 1.0.0-beta7 -runtime CLR -arch x86 -a default

    foreach ($file in (Get-ChildItem "src" -rec))
    {
        RestoreXProj($file)
    }
}

$artifacts = Join-Path $executingScriptDirectory "artifacts"
$artifactsSrc = Join-Path $artifacts "src"
$artifactsTest = Join-Path $artifacts "test"

foreach ($file in (Get-ChildItem "src" -rec))
{
    $ext = [System.IO.Path]::GetExtension($file.FullName)

    if ($ext -eq ".xproj")
    {
        $srcDir = [System.IO.Path]::GetDirectoryName($file.FullName)
        & dnu pack "$($srcDir)" --out $artifactsSrc

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


popd