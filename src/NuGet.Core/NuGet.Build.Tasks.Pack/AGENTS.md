# NuGet.Build.Tasks.Pack

**Location**: src\NuGet.Core\NuGet.Build.Tasks.Pack\
**Shipping**: true | **Tests**: test/NuGet.Core.Tests/NuGet.Build.Tasks.Pack.Test/

## Purpose
MSBuild task assembly for dotnet pack. Public surface: 5 tasks + 2 interfaces managing 82+ MSBuild properties (PackageId, IncludeSymbols, License, Readme, TargetFrameworks, etc.).

## Public APIs
- **PackTask** (IPackTaskRequest<ITaskItem>): Main entry point, 85+ settable properties
- **GetPackOutputItemsTask**: Predicts output filenames from PackageId/Version
- **GetProjectReferencesFromAssetsFileTask**: Resolves project dependencies from assets.json
- **IsPackableFalseWarningTask**: Validates IsPackable=true
- **IPackTaskLogic**: Orchestration interface (GetPackArgs, GetPackageBuilder, GetPackCommandRunner, BuildPackage)
- **IPackTaskRequest<TItem>**: Input contract defined in `IPackTaskRequest.cs`

## Critical Invariants
**Multi-targeting** (csproj:4): net472 + net8.0 → Conditional refs (net472: Pack=false GAC refs; .NET Core: PackageReference ExcludeAssets=runtime)

**Packaging** (csproj:8–19): Shipping=true, DevelopmentDependency=true, IncludeSatelliteOutputInPack=true (13 xlf), SuppressDependenciesWhenPacking=true, PreserveCompilationContext=true

**Output Filtering** (NuGet.Build.Tasks.Pack.targets): AllowedExtensions={.dll, .exe, .winmd, .json, .pri, .xml}; Symbols={.pdb} or {.pdb + .mdb}

## Tests
PackTaskTests (mock IPackTaskLogic), PackTaskLogicTests (GetPackArgs chain), GetPackOutputItemsTaskTests (filename generation + nuspec preprocessing)

## Validation

```powershell
cd src\NuGet.Core\NuGet.Build.Tasks.Pack
dotnet restore
dotnet build --no-restore -c Release
cd ..\..\..\test\NuGet.Core.Tests\NuGet.Build.Tasks.Pack.Test
dotnet test test\NuGet.Core.Tests\NuGet.Build.Tasks.Pack.Test\NuGet.Build.Tasks.Pack.Test.csproj --logger console --filter "FullyQualifiedName~PackTask"
```

## Uncertainties
- DEBUG_PACK_TASK env var (PackTask.cs) undocumented; DEBUG-only debugger launch
- DeterministicTimestamp (PackTaskLogic:231) requires external clock provider; interaction with reproducible builds unclear
- MSBuild.Utilities.v4.0 (net472 only) GAC dependency not validated in CI
- Symbol format resolution (SymbolPackageFormat) delegates to NuGet.Commands; coupling unclear
