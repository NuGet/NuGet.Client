![NuGet logo](https://raw.githubusercontent.com/NuGet/Home/master/resources/nuget.png)

-----

#NuGet Client Tools

This repo contains the following clients:
  * NuGet command-line tool 3.0 and higher
  * Visual Studio 2015 Extension
  * Visual Studio "15" Extension
  * PowerShell CmdLets

### Build Status

[![Build status](https://ci.appveyor.com/api/projects/status/1encuvwjo6k2sq68?svg=true)](https://ci.appveyor.com/project/NuGetTeam/nuget-client)

## Open Source Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## How to build NuGet VisualStudio extension

###Prerequisites:
- [VisualStudio 2015](https://www.visualstudio.com/)
- [VisualStudio 2015 SDK](https://msdn.microsoft.com/en-us/library/bb166441.aspx)
- [Windows 10 SDK](https://dev.windows.com/en-US/downloads/windows-10-sdk)
- Git
- Powershell

###Steps to build the clients tools repo:
- Clone [NuGet.Client](https://github.com/nuget/nuget.client) Repo by running the following command `git clone https://github.com/NuGet/NuGet.Client`
- Start powershell
- CD into the clone repo directory
- Run `.\build.ps1 -CleanCache`

######In case you have build issues please clean the local repo using `git clean -xdf` and retry building

####Notable .\build.ps1 switches
- `-SkipVS14` - skips building binaries targeting Visual Studio "14" (released as Visual Studio 2015)
- `-SkipVS15` - skips building binaries targeting Visual Studio "15"

Note that if only one of Visual Studio 2015 (Dev14) or Visual Studio 15 (Dev15) is installed, neither of the above switches is necessary - the script will build according to the installed version.

- `-SkipXProj` - skips building the NuGet.Core XProj projects
- `-SkipTests` - builds binaries, skips running tests
- `-SkipRestore` - builds without restoring first
- `-SkipSubmodules` - builds without updating submodules
- `-SkipILMerge` - builds without creating an ILMerged nuget.exe
- `-Fast` - skips tests, submodule updates, and ILMerged nuget.exe

###Build Artifacts
- (RepoRootFolder)\Artifacts - this folder will contain the Vsix and NuGet command-line
- (RepoRootFolder)\Nupkgs - this folder will contain all our projects packages