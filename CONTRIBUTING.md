# Setup

After you clone this repository run build.ps1, you might have to run build.ps1.

Note that build.ps1 is the only process producing all nuget packages and artifacts, building from visual studio is good for development purposes, but is not enough at the moment.

### Build.ps1 is responsible for the following steps:

1. Update submodules
1. Restore nuget packages
1. Build
1. ilmerge nuget.exe
1. Running all unit tests

# Building in Visual Studio

Follow the instructions in the README.

# Contributing

1. [Open an issue here](https://github.com/NuGet/Home/issues) and get some feedback from the NuGet team.
1. Create a branch. Base it on the `dev` branch.
1. Add unit tests (inside the `test` subfolder of the `NuGet.Core` solution).
1. Make sure all tests pass (via `Test -> Run -> All Tests`).
1. Create a [pull request](https://github.com/NuGet/NuGet.Client/pulls).
1. _One-time_: Sign the contributor license agreement, if you haven't signed it before. The [.NET Foundation Bot](https://github.com/dnfclas) will comment on the pull request you just created and guide you on how to sign the CLA.
1. Consider submitting a doc pull request to the [nugetdocs](https://github.com/NuGet/NuGetDocs/tree/master/NuGet.Docs) repo, if this is a new feature or behavior change.