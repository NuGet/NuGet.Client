![NuGet logo](https://raw.githubusercontent.com/NuGet/Home/master/resources/nuget.png)

-----

# NuGet Client Tools

This repo contains the following clients:
  * [NuGet CLI](https://docs.nuget.org/ndocs/tools/nuget.exe-cli-reference)
  * [NuGet Package Manager for Visual Studio 2015/2017](https://docs.nuget.org/ndocs/tools/package-manager-ui)
  * [PowerShell CmdLets](https://docs.nuget.org/ndocs/tools/powershell-reference)

### Build Status

[![Build status](https://ci.appveyor.com/api/projects/status/1encuvwjo6k2sq68?svg=true)](https://ci.appveyor.com/project/NuGetTeam/nuget-client)

## Open Source Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## How to build NuGet client tools

### Prerequisites
- [Visual Studio "15" Enterprise Preview 5](https://aka.ms/vs/15/preview/vs_enterprise)
  with following workloads:
    - .NET desktop development
    - Desktop development with C++
    - Visual Studio extension development.
- [Visual Studio 2015 Update 3](https://go.microsoft.com/fwlink/?LinkId=691129)
  with Visual Studio Extensibility Tools
- [Windows 10 SDK](https://dev.windows.com/en-US/downloads/windows-10-sdk)
- Git
- Windows Powershell v3.0+

### Steps to build NuGet client tools

1. Clone [NuGet/NuGet.Client](https://github.com/nuget/nuget.client) repository

    `git clone https://github.com/NuGet/NuGet.Client`

2. Start PowerShell. CD into the cloned repository directory.
3. Run configuration script

    `.\configure.ps1`

4. Build with

    `.\build.ps1`

5. Run unit-tests

    `.\runTests.ps1 -SkipFuncTests`

6. Run all test-suites if inside Microsoft corpnet

    `.\runTests.ps1`



> In case you have build issues try cleaning the local repository using `git clean -xdf` and retry steps 3 and 4.

#### Notable `build.ps1` switches
- `-SkipVS14` - skips building binaries targeting Visual Studio "14" (released as Visual Studio 2015)
- `-SkipVS15` - skips building binaries targeting Visual Studio "15"

> Note that if only one of Visual Studio 2015 (VS14) or Visual Studio 2017 (VS15) is installed, neither of the above switches is necessary - the script will build according to the installed version.

- `-SkipXProj` - skips building the NuGet.Core XProj projects.
- `-Fast` - runs minimal incremental build. Skips end-to-end packaging step.

> Reveal all script parameters and switches by running
  ```posh
  Get-Help .\build.ps1 -detailed
  ```

### Build artifacts location
- `$(NuGetClientRoot)\Artifacts` - this folder will contain the Package Manager extension (`NuGet.Tools.vsix`) and NuGet command-line client application (`nuget.exe`)
- `$(NuGetClientRoot)\Nupkgs` - this folder will contain all our projects packages