# Setup

After you clone this repository run build.ps1, you might have to run build.ps1 -CleanCache if the build doesn't work (because we sometimes update temporary packages in place).

Note that build.ps1 is the only process producing all nuget packages and artifacts, building from visual studio is good for development purposes, but is not enough at the moment.

### Build.ps1 is responsible for the following steps:

1. Update submodules
1. Restore nuget packages
1. Build
1. ilmerge nuget.exe
1. Running all unit tests

### Note that we have two solutions:

1. NuGet.core - An asp.net 5 based projects, that are cross compiled for core clr and are intented to run on unix and mac.
1. NuGet.Client - A csproj based solution that is producing nuget.exe and the nuget extension. This project is based on nuget packages produced by the nuget.core solution.

Right now there is no way for a csproj to depend directly on an xproj (asp.net 5) project. Once that is available (and we are working on it), we will merge the two solutions.

# Building in Visual Studio

Install [Visual Studio 2015](https://www.visualstudio.com/) (preferrable also Update 1 or later) and [ASP.NET 5 RC](https://get.asp.net/) (or later).

Open [`NuGet.Core.sln`](NuGet.Core.sln) (or [`NuGet.Client.sln`](NuGet.Client.sln)) in Visual Studio and wait until it restores all packages.

Once packages are restored, you should be able to build the project via `Build -> Build Solution`.

# Contributing

1. [Open an issue here](https://github.com/NuGet/Home/issues) and get some feedback from the NuGet team.
1. Create a branch. Base it on the `dev` branch.
1. Add unit tests (inside the `test` subfolder of the `NuGet.Core` solution).
1. Make sure all tests pass (via `Test -> Run -> All Tests`).
1. Create a [pull request](https://github.com/NuGet/NuGet.Client/pulls).
1. _One-time_: Sign the contributor license agreement, if you haven't signed it before. The [.NET Foundation Bot](https://github.com/dnfclas) will comment on the pull request you just created and guide you on how to sign the CLA.
1. Consider submitting a doc pull request to the [nugetdocs](https://github.com/NuGet/NuGetDocs/tree/master/NuGet.Docs) repo, if this is a new feature or behavior change.