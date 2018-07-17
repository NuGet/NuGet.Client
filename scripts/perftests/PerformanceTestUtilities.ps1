    function DownloadNuGetExe([string]$version, [string]$downloadDirectory)
    {        
        $NuGetExeUriRoot = "https://dist.nuget.org/win-x86-commandline/v"
        $NuGetExeSuffix = "/nuget.exe"

        $url = $NuGetExeUriRoot + $version + $NuGetExeSuffix
        $Path =  $downloadDirectory + "\" + $version
        $ExePath = $Path + "\nuget.exe"

        if (!(Test-Path($ExePath)))
        {
            Log "Downloading $url to $ExePath" "Green"
            New-Item -ItemType Directory -Force -Path  $Path
            Invoke-WebRequest -Uri $url -OutFile $ExePath
        }
        return GetAbsolutePath $ExePath
    }

    function Log([string]$logStatement, [string]$color)
    {
        if(-not ([string]::IsNullOrEmpty($color)))
        {
            Write-Host "$($(Get-Date).ToString()): $logStatement" -ForegroundColor $color
        }
        else
        { 
            Write-Host "$($(Get-Date).ToString()): $logStatement"
        }
    }

    function GetAbsolutePath([string]$Path)
    {
    $Path = [System.IO.Path]::Combine(((pwd).Path), ($Path));
    $Path = [System.IO.Path]::GetFullPath($Path);
    return $Path;
    }

    function IIf($If, $Right, $Wrong) {
        if ($If)
        {
            $Right
        } 
        else 
        {
            $Wrong
        }
    }

    function OutFileWithCreateFolders([string]$path, [string]$content){
        $folder = [System.IO.Path]::GetDirectoryName($path)
        If(!(test-path $folder))
        {
            & New-Item -ItemType Directory -Force -Path $folder > $null
        }
        Add-Content -Path $path -Value $content
    }

    function GetAllPackagesInGlobalPackagesFolder([string]$packagesFolder)
    {
        if(Test-Path $packagesFolder){
            $packages = Get-ChildItem $packagesFolder\*.nupkg -Recurse
            return $packages
        }
        return $null
    }

    function GetFiles([string]$folder)
    {
        if(Test-Path $folder){
            $files = Get-ChildItem $folder -recurse
            return $files
        }
        return $null
    }

    function GetNuGetFoldersPath()
    {
        $nugetFolder = [System.IO.Path]::Combine($env:UserProfile, "np")
        return $nugetFolder
    }