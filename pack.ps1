param (
	# the additional package source used to restore packages.
	# if it's a directory, the generated packages are also copied there	
	[string]$PushTarget,

    [ValidateSet("debug", "release")][string]$Configuration="release",
    [switch]$SkipTests,
    [string]$PFXPath,
    [switch]$DelaySign,
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$MsbuildParameters = ''
)

# build the specified project to create the nupkg
function Pack(
	# the project from which the nupkg is created 
	[string]$ProjectFile,
	
	# the id of the package
	[string]$Id, 
	
	# true if -IncludeReferencedProjects is passed to nuget pack command
	[Boolean]$IncludeReferencedProjects)
{
    # assembly containing the release file version to use for the package
    $workingDir = (Get-Item -Path ".\" -Verbose).FullName;

    # build the csproj and dll full paths
    $projectPath = Join-Path $workingDir $ProjectFile    

    Write-Host "Project to build: $projectPath" -ForegroundColor Cyan
    Write-Host "Package version: $version" -ForegroundColor Cyan

    # create the output folder
    if ((Test-Path nupkgs) -eq 0) {
        New-Item -ItemType directory -Path nupkgs | Out-Null
    }

    # Pack
	Write-Host "Project path is $ProjectPath"
	if ($IncludeReferencedProjects) {		
		.\.nuget\nuget.exe pack $projectPath -Properties configuration=$Configuration -symbols -OutputDirectory nupkgs -version $version -IncludeReferencedProjects 
	}
	else {
		.\.nuget\nuget.exe pack $projectPath -Properties configuration=$Configuration -symbols -OutputDirectory nupkgs -version $version
	}
}

function Build()
{
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

        if ($DelaySign)
        {
            $env:NUGET_DELAYSIGN="true"
        }
    }

	$env:NUGET_PUSH_TARGET="$PushTarget"
    Write-Host "Building! configuration: $Configuration" -ForegroundColor Cyan
    
    $msbuildExe = "${env:ProgramFiles(x86)}\MSBuild\14.0\Bin\msbuild.exe"
    
    & $msbuildExe "build\build.msbuild" "/p:Configuration=$Configuration" /p:EnableCodeAnalysis=true /m /v:M  /fl /flp:v=D $msbuildParameters
	if ($lastexitcode -ne 0) 
	{		
	  	throw "Build failed"
	}	
    Write-Host "Build complete! configuration: $Configuration" -ForegroundColor Cyan
}

Build
Pack "src\PackageManagement\PackageManagement.csproj" "NuGet.PackageManagement" $true
Pack "src\PackageManagement.UI\PackageManagement.UI.csproj" "NuGet.PackageManagement.UI" $false

# copy packages to $PushTarget if $PushTarget is a directory
if ($PushTarget -and (Test-Path $PushTarget))
{
	Copy-Item "nupkgs\*.nupkg" $PushTarget
}