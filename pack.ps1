param (
    [string]$PushTarget,
    [ValidateSet("debug", "release")][string]$Configuration="release",
    [switch]$SkipTests,
    [switch]$SkipBuild,
    [string]$PFXPath,
    [switch]$DelaySign,
    [switch]$Stable,
    [string]$Version,
    [switch]$NoLock
)

function BuildAndPack([string]$Id)
{
    # build
    if (!$SkipBuild)
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

        Write-Host "Building! configuration: $Configuration" -ForegroundColor Cyan
        Start-Process "cmd.exe" "/c build.cmd /p:Configuration=$Configuration" -Wait -NoNewWindow
        Write-Host "Build complete! configuration: $Configuration" -ForegroundColor Cyan

        if ($PushTarget)
        {
            Write-Host "Updating package references for our own packages"
            & .\.nuget\nuget.exe update Client.v3.sln -source "$PushTarget"

            Write-Host "Now, building again to consume the updated packages"
            Start-Process "cmd.exe" "/c build.cmd /p:Configuration=$Configuration" -Wait -NoNewWindow
        }

    }

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

    # find the current git branch
    $gitBranch = "ci"

    git branch | foreach {
        if ($_ -match "^\*(.*)") {
            $gitBranch = $matches[1].Trim()
        }
    }

    # prerelease labels can have a max length of 20
    # shorten the branch to 8 chars if needed
    if ($gitBranch.Length -gt 8) {
        $gitBranch = $gitBranch.SubString(0, 8)
    }

    Write-Host "Git branch: $gitBranch" 

    if (!$Version) {
        # find the release version from the target assembly
        $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($primaryAssemblyPath).FileVersion

        if (!$version) {
            Write-Error "Unable to find the file version!"
            exit 1
        }

        $now = [System.DateTime]::UtcNow

        # (git branch)-(last digit of the year)(day of year)(hour)(minute)
        $version = $version.TrimEnd('0').TrimEnd('.')

        if (!$Stable)
        {
            # prerelease labels can have a max length of 20
            $now = [System.DateTime]::UtcNow
            $version += "-" + $now.ToString("pre-yyyyMMddHHmmss")

            if ($Configuration -eq "debug")
            {
                $version += "-d"
            }
        }
    }

    Write-Host "Package version: $version" -ForegroundColor Cyan

    # create the output folder
    if ((Test-Path nupkgs) -eq 0) {
        New-Item -ItemType directory -Path nupkgs | Out-Null
    }

    # Pack
    .\.nuget\nuget.exe pack $projectPath -Properties configuration=$Configuration -symbols -OutputDirectory nupkgs -version $version

    # Find the path of the nupkg we just built
    $nupkgPath = Get-ChildItem .\nupkgs -filter "$Id.$version.nupkg" | % { $_.FullName }

    Write-Host $nupkgPath -ForegroundColor Cyan

    if (!$Stable -And !$NoLock)
    {
        Write-Host "Locking dependencies down"
        .\tools\NupkgLock\NupkgLock.exe "$Id.nuspec" $nupkgPath
    }

    if ($PushTarget)
    {
        Write-Host "Pushing: $nupkgPath" -ForegroundColor Cyan
        # use nuget.exe setApiKey <key> before running this
        .\.nuget\nuget.exe push $nupkgPath -source "$PushTarget"
    }
    else
    {
        Write-Warning "Package not uploaded. Specify -PushTarget to upload this package"
    }
}

BuildAndPack("NuGet.Protocol.Types")

if (!$SkipBuild)
{
    Write-Host "Updating the NuGet.Protocol.Types package reference for NuGet.Protocol"

    if ($PushTarget)
    {
        $updateSource = $PushTarget
    }
    else
    {
        $updateSource = Join-Path "." "nupkgs" -resolve
    }

    & nuget.exe update "Client.v3.sln" -id "NuGet.Protocol.Types" -source "$updateSource" -repositoryPath ".\packages"
}

BuildAndPack("NuGet.Protocol")