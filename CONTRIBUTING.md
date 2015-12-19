# Init

After you clone this repository, make sure you update submodules with the following command:

```
git submodule init && git submodule update
```

# Building

Install [Visual Studio 2015](https://www.visualstudio.com/) (or later) and [ASP.NET 5 RC](https://get.asp.net/) (or later).

Open [`NuGet.Core.sln`](NuGet.Core.sln) in Visual Studio and wait until it restores all packages.

Once packages are restored, you should be able to build the project via `Build -> Build Solution`.

# Pull requests

The main development branch is `dev`. When creating new features, please branch off `dev`.

Pull requests should typically use `dev` as the base.