# Getting Started

## Prerequisites

- [Visual Studio 2019](https://www.visualstudio.com)
  with following workloads:
  - .NET Core Cross Platform Development
  - .NET desktop development
  - Visual Studio extension development.
  - C++ development
- [Windows 10 SDK](https://dev.windows.com/en-US/downloads/windows-10-sdk)
- Git
- Windows Powershell v3.0+

> Note that you can work on the NuGet.Client repo with Visual Studio 2017, but you will be unable to test the Visual Studio extension.

## Code

Non NuGet members contribute to this repository is via the [fork model](https://help.github.com/articles/fork-a-repo/). Contributors push changes to their own "forked" version of NuGet.Client, and then submit a pull request into it requesting those changes be merged.

To get started:

1. Fork the repo.

2. From a git enable terminal, run (replacing _[user-name]_ with your GitHub user name):

```console
\> git clone https://github.com/[user-name]/NuGet.Client
\> cd NuGet.Client
\NuGet.Client> git remote add upstream https://github.com/NuGet/NuGet.Client
\NuGet.Client> git remote set-url --push upstream no_push
```

After running above, `git remote -v` should show something similar to the following:

```console
\NuGet.Client> git remote -v

origin  https://github.com/[user-name]/NuGet.Client (fetch)
origin  https://github.com/[user-name]/NuGet.Client (push)
upstream        https://github.com/NuGet/NuGet.Client (fetch)
upstream        no_push (push)
```

### NuGet team members

NuGet members may contribute directly to the main remote.

## Build

1. Clone the NuGet.Client repository.

1. Start PowerShell. CD into the cloned repository directory.

1. Run the configuration script

    `.\configure.ps1`

1. Build with

    `.\build.ps1 -SkipUnitTest`

   Or Build and Unit test with

   `.\build.ps1`

1. Run unit and functional tests if inside Microsoft corpnet with

    `.\runTests.ps1`

> In case you have build issues try cleaning the local repository using `git clean -xdf` and retry steps 3 and 4.

### Notable `build.ps1` switches

- `-SkipUnitTest` - skips running unit tests.
- `-Fast` - runs minimal incremental build. Skips end-to-end packaging step.

> Reveal all script parameters and switches by running
  Get-Help .\build.ps1 -detailed

### Build artifacts location

- `$(NuGetClientRoot)\artifacts\VS15` - this folder will contain the Package Manager extension (`NuGet.Tools.vsix`) and NuGet command-line client application (`NuGet.exe`)
- `$(NuGetClientRoot)\artifacts\nupkgs` - this folder will contain all our projects packages

## Resources for NuGet.Client development

- [Workflow](workflow.md)
- [Coding Guidelines](coding-guidelines.md)
