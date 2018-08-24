# Contains all the utility methods used by the performance tests.

    # Downloads a nuget exe in the given directory.
    # The exe will be found at $directory/$version/nuget.exe
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

    
    # The format of the URL is assumed to be https://github.com/NuGet/NuGet.Client.git. The result would be NuGet-Client-git
    function GenerateNameFromGitUrl([string]$gitUrl){
        return $gitUrl.Substring($($gitUrl.LastIndexOf('/') + 1)).Replace('.','-')
    }

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
    function GetClientName([string]$nugetClient)
    {
        return $nugetClient.Substring($($nugetClient.LastIndexOf([System.IO.Path]::DirectorySeparatorChar) + 1))
    }

    function IsClientDotnetExe([string]$nugetClient)
    {
        return $nugetClient.EndsWith("dotnet.exe")
    }

    # Downloads the repository at the given path.
    function DownloadRepository([string]$repository, [string]$commitHash, [string]$sourceDirectoryPath)
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
    }
    
    # Find the appropriate solution file for the repository. Looks for a solution file matching the repo name, 
    # if not it takes the first available sln file in the repo. 
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

    # Given a repository and a hash, checks out the revision in the given source directory. The return is a solution file if found. 
    function SetupGitRepository([string]$repository, [string]$commitHash, [string]$sourceDirectoryPath)
    {
        DownloadRepository $repository $commitHash $sourceDirectoryPath
        $solutionFile = GetSolutionFile $repository $sourceDirectoryPath
        Log "Completed the repository setup. The solution file is $solutionFile" -color "Green"
        return $solutionFile
    }

    # runs locals clear all with the given client
    function LocalsClearAll([string]$nugetClient)
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
    }

    # Gets the client version
    function GetClientVersion($nugetClient)
    {
        $nugetClient = GetAbsolutePath $nugetClient
        if(IsClientDotnetExe $nugetClient)
        {
            $version = . $nugetClient --version
            Log "$version"
            return $version
        } 
        else 
        {
            $versionQuery = . $nugetClient
            $version = $(($versionQuery -split '\n')[0]).Substring(15)
            return $version
        }
    }

    # Gets the pretermined nuget folders path where all of the throwable data from the tests will be put.
    function GetNuGetFoldersPath()
    {
        $nugetFolder = [System.IO.Path]::Combine($env:UserProfile, "np")
        return $nugetFolder
    }

    # Sets up the global packages folder, http cache and plugin caches and cleans them before starting.
    # TODO NK - How about temp?
    function SetupNuGetFolders([string]$nugetClient)
    {
        $nugetFolders = GetNuGetFoldersPath
        $Env:NUGET_PACKAGES = [System.IO.Path]::Combine($nugetFolders, "gpf")
        $Env:NUGET_HTTP_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "hcp")
        $Env:NUGET_PLUGINS_CACHE_PATH = [System.IO.Path]::Combine($nugetFolders, "pcp")
        LocalsClearAll $nugetClient
    }

    # Cleanup the nuget folders and delete the nuget folders path. 
    # This should only be invoked by the the performance tests
    # TODO NK - Does this delete the sources as well? Should not. 
    function CleanNuGetFolders([string]$nugetClient)
    {
        LocalsClearAll $nugetClient
        $nugetFolders = GetNuGetFoldersPath

        & Remove-Item -r $nugetFolders -force > $null
        [Environment]::SetEnvironmentVariable("NUGET_PACKAGES",$null)
        [Environment]::SetEnvironmentVariable("NUGET_HTTP_CACHE_PATH",$null)
        [Environment]::SetEnvironmentVariable("NUGET_PLUGINS_CACHE_PATH",$null)
    }

    # Given a repository, a client and directories for the results/logs, runs the configured performance tests.
    function RunPerformanceTestsOnGitRepository([string]$nugetClient, [string]$sourceRootDirectory, [string]$repoUrl,  [string]$commitHash, [string]$resultsDirPath, [string]$logsPath)
    {
        $repoName = GenerateNameFromGitUrl $repoUrl
        $solutionFilePath = SetupGitRepository $repoUrl $commitHash $([System.IO.Path]::Combine($sourceRootDirectory, $repoName))

        $resultsFilePath = [System.IO.Path]::Combine($resultsDirPath, "$repoName.csv")
        SetupNuGetFolders $nugetClient

        . "$PSScriptRoot\RunPerformanceTests.ps1" $nugetClient $solutionFilePath $resultsFilePath $logsPath
    }