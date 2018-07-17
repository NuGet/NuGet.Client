    function DownloadNuGetExe([string]$version, [string]$downloadDirectory)
    {        
        $NuGetExeUriRoot = "https://dist.nuget.org/win-x86-commandline/v"
        $NuGetExeSuffix = "/nuget.exe"

        $url = $NuGetExeUriRoot + $version + $NuGetExeSuffix
        $Path =  $downloadDirectory + "\" + $version
        $ExePath = $Path + "\nuget.exe"

        if (!(Test-Path($ExePath)))
        {
            Log "Downloading $url to $ExePath" -color "Green"
            New-Item -ItemType Directory -Force -Path $Path > $null
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

    function RepositorySetup([string]$nugetClientFilePath, [string]$repository, [string]$branch, [string]$sourceDirectoryPath)
    {
        if(!(Test-Path $sourceDirectoryPath))
        {
                git clone -b $branch --single-branch $repository $sourceDirectoryPath
        }
        else 
        {
                Log "Skipping the cloning of $repository as $sourceDirectoryPath is not empty" -color "Yellow"
        }
        $nugetFolders = GetNuGetFoldersPath
        $Env:NUGET_PACKAGES = [System.IO.Path]::Combine($nugetFolders, "gpf")
        $Env:NUGET_HTTP_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "hcp")
        $Env:NUGET_PLUGINS_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "pcp")

        . $nugetClientFilePath locals -clear all -Verbosity quiet

        $solutionFile = (Get-ChildItem $sourceDirectoryPath *.sln)[0] | Select-Object -f 1 | Select-Object -ExpandProperty FullName

        Log "Completed the repository setup. The solution file is $solutionFile" -color "Green"
        return $solutionFile
    }

    function RunTest([string]$_nugetClientFilePath, [string]$_repositoryUrl, [string]$_branchName, [string]$_resultsDirectoryPath, [string]$_logsPath){
        $repoName = GenerateNameFromGitUrl $_repositoryUrl
        $sourceDirectoryPath = [System.IO.Path]::Combine($testDirectoryPath, $repoName)
        $solutionFile = RepositorySetup $_nugetClientFilePath $_repositoryUrl $_branchName $sourceDirectoryPath
        $resultsFilePath = [System.IO.Path]::Combine($_resultsDirectoryPath, "$repoName.csv")
        . "$PSScriptRoot\RunPerformanceTests.ps1" $_nugetClientFilePath $solutionFile $resultsFilePath $_logsPath
    }

    # The format of the URL is assumed to be https://github.com/NuGet/NuGet.Client.git. The result would be NuGet-Client-git
    function GenerateNameFromGitUrl([string]$gitUrl){
        return $gitUrl.Substring($($gitUrl.LastIndexOf('/') + 1)).Replace('.','-')
    }