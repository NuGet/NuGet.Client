# Setup

After you clone this repository, make sure you update submodules with the following command:

```
git submodule init && git submodule update
```

# Building

Install [Visual Studio 2015](https://www.visualstudio.com/) (or later) and [ASP.NET 5 RC](https://get.asp.net/) (or later).

Open [`NuGet.Core.sln`](NuGet.Core.sln) (or [`NuGet.Client.sln`](NuGet.Client.sln)) in Visual Studio and wait until it restores all packages.

Once packages are restored, you should be able to build the project via `Build -> Build Solution`.

# Contributing

1. [Open an issue here](https://github.com/NuGet/Home/issues) and get some feedback from the NuGet team.
1. Create a branch. Base it on the `dev` branch.
1. Add unit tests (inside the `test` subfolder of the `NuGet.Core` solution).
1. Make sure all tests pass (via `Test -> Run -> All Tests`).
1. Create a [pull request](https://github.com/NuGet/NuGet.Client/pulls).
1. _One-time_: Sign the contributor license agreement, if you haven't signed it before. The [.NET Foundation Bot](https://github.com/dnfclas) will comment on the pull request you just created and guide you on how to sign the CLA.