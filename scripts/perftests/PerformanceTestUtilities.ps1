# Contains all the utility methods used by the performance tests.

    # Appends the log time in front of the log statement with the color specified. 
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

    # Given a relative path, gets the absolute path from the current directory
    function GetAbsolutePath([string]$Path)
    {
        $Path = [System.IO.Path]::Combine(((pwd).Path), ($Path));
        $Path = [System.IO.Path]::GetFullPath($Path);
        return $Path;
    }

    # Writes the content to the given path. Creates the folder structure if needed
    function OutFileWithCreateFolders([string]$path, [string]$content){
        $folder = [System.IO.Path]::GetDirectoryName($path)
        If(!(Test-Path $folder))
        {
            & New-Item -ItemType Directory -Force -Path $folder > $null
        }
        Add-Content -Path $path -Value $content
    }

    # Gets a list of all the nupkgs recursively in the global packages folder
    function GetAllPackagesInGlobalPackagesFolder([string]$packagesFolder)
    {
        if(Test-Path $packagesFolder)
        {
            $packages = Get-ChildItem $packagesFolder\*.nupkg -Recurse
            return $packages
        }
        return $null
    }

    # Gets a list of all the files resursively in the given folder
    function GetFiles([string]$folder)
    {
        if(Test-Path $folder)
        {
            $files = Get-ChildItem $folder -recurse
            return $files
        }
        return $null
    }

    # Determines if the client is dotnet.exe by checking the path.
    function IsClientDotnetExe([string]$nugetClient)
    {
        return $nugetClient.EndsWith("dotnet.exe")
    }

    # Gets the pretermined nuget folders path where all of the throwable data from the tests will be put.
    function GetNuGetFoldersPath()
    {
        $nugetFolder = [System.IO.Path]::Combine($env:UserProfile, "np")
        return $nugetFolder
    }
    function SetupNuGetFolders([string]$_nugetClientFilePath)
    {
        $nugetFolders = GetNuGetFoldersPath
        $Env:NUGET_PACKAGES = [System.IO.Path]::Combine($nugetFolders, "gpf")
        $Env:NUGET_HTTP_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "hcp")
        $Env:NUGET_PLUGINS_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "pcp")

        if($(IsClientDotnetExe $_nugetClientFilePath))
        {
            . $_nugetClientFilePath nuget locals -c all  *>>$null
        } 
        else 
        {
            . $_nugetClientFilePath locals -clear all -Verbosity quiet
        }
    }

    function SetupGitRepository([string]$repository, [string]$commitHash, [string]$sourceDirectoryPath)
    {
        if(!(Test-Path $sourceDirectoryPath))
        {
                git clone $repository $sourceDirectoryPath
                git -c $sourceDirectoryPath checkout $commitHash
        }
        else 
        {
                Log "Skipping the cloning of $repository as $sourceDirectoryPath is not empty" -color "Yellow"
        }

        $solutionFile = GetSolutionFile $repository $sourceDirectoryPath

        Log "Completed the repository setup. The solution file is $solutionFile" -color "Green"
        return $solutionFile
    }

    function GetSolutionFile([string]$repository,[string]$sourceDirectoryPath) {

        $gitRepoName = $repository.Substring($($repository.LastIndexOf('/') + 1))
        $potentialSolutionFile = [System.IO.Path]::Combine($sourceDirectoryPath, "$($gitRepoName.Substring(0, $gitRepoName.Length - 4)).sln")

        if(Test-Path $potentialSolutionFile)
        {
            $solutionFile = $potentialSolutionFile
        } 
        else 
        {
            $possibleSln = Get-ChildItem $sourceDirectoryPath *.sln
            if($possibleSln.Length -eq 0)
            {
                Log "No solution files found in $sourceDirectoryPath" "red"
            } 
            else 
            {
            $solutionFile = $possibleSln[0] | Select-Object -f 1 | Select-Object -ExpandProperty FullName
            }
        }
        return $solutionFile;
    }

    function RunPerformanceTestsOnGitRepository([string]$nugetClient, [string]$sourceRootDirectory, [string]$repoUrl,  [string]$commitHash, [string]$resultsDirPath, [string]$logsPath)
    {
        $repoName = GenerateNameFromGitUrl $repoUrl
        $resultsFilePath = [System.IO.Path]::Combine($resultsDirPath, "$repoName.csv")
        $solutionFilePath = SetupGitRepository $repoUrl $commitHash $([System.IO.Path]::Combine($sourceRootDirectory, $repoName))
        SetupNuGetFolders $nugetClient
        . "$PSScriptRoot\RunPerformanceTests.ps1" $nugetClient $solutionFilePath $resultsFilePath $logsPath
    }
    

    # The format of the URL is assumed to be https://github.com/NuGet/NuGet.Client.git. The result would be NuGet-Client-git
    function GenerateNameFromGitUrl([string]$gitUrl){
        return $gitUrl.Substring($($gitUrl.LastIndexOf('/') + 1)).Replace('.','-')
    }

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

    function CleanNuGetFolders([string]$nugetClient)
    {
        $nugetClient = GetAbsolutePath $nugetClient
        if($(IsClientDotnetExe $nugetClient))
        {
            . $nugetClient nuget locals -c all *>>$null
        } 
        else 
        {
            . $nugetClient locals -clear all -Verbosity quiet
        }
        $nugetFolders = GetNuGetFoldersPath
        & Remove-Item -r $nugetFolders -force > $null
        [Environment]::SetEnvironmentVariable("NUGET_PACKAGES",$null)
        [Environment]::SetEnvironmentVariable("NUGET_HTTP_CACHE_PATH",$null)
        [Environment]::SetEnvironmentVariable("NUGET_PLUGINS_CACHE_PATH",$null)
    }