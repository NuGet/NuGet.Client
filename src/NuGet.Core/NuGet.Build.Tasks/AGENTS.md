# NuGet.Build.Tasks

## Architecture

**Task Exports (17 public MSBuild tasks):** RestoreTask, RestoreTaskEx, GenerateRestoreGraphFileTask, GetCentralPackageVersionsTask, GetProjectTargetFrameworksTask, GetRestorePackageReferencesTask, GetRestoreProjectReferencesTask, GetRestoreSolutionProjectsTask, GetRestoreDotnetCliToolsTask, GetRestoreFrameworkReferencesTask, GetRestoreNuGetAuditSuppressionsTask, GetRestorePackageDownloadsTask, GetRestorePrunedPackageReferencesTask, GetReferenceNearestTargetFrameworkTask, WriteRestoreGraphTask, WarnForInvalidProjectsTask, CheckForDuplicateNuGetItemsTask.

**Shipped Targets** (three, in NuGet package runtimes\any\native): NuGet.targets (restore orchestration and DG file generation), NuGet.RestoreEx.targets (static-graph task invocation), NuGet.props (Directory.Packages.props centralized loading).

**Static-Graph Base:** StaticGraphRestoreTaskBase spawns out-of-proc NuGet.Build.Tasks.Console.exe via MSBuildBinPath environment and cancellation token coordination.

**Multi-Targeting:** TargetFrameworks property passed to extraction tasks; framework-conditional IS_DESKTOP assembly references for v4.0 vs Core; VSIX build overrides TargetFrameworksExe with NETFXTargetFramework.

**Localization:** Strings.resx (PublicResXFileCodeGenerator) + 13 .xlf files (zh-Hant, zh-Hans, tr, ru, pt-BR, pl, ko, ja, it, fr, es, de, cs).

## High-Risk Invariants

- RestoreTaskEx must remain public sealed; concrete StaticGraphRestoreTaskBase implementation required by NuGet.RestoreEx.targets UsingTask.
- NuGet.{targets,props,RestoreEx.targets} file names and runtimes\any\native pack paths are solution-level restore entry points; renaming or removal breaks restore orchestration.
- Cancellation tokens (RestoreTask._cts, StaticGraphRestoreTaskBase._cancellationTokenSource) implement ICancelableTask.Cancel() lifecycle; premature disposal causes task hang.
- Out-of-proc console invocation requires MSBuildBinPath; null or invalid path causes silent process failure.

## Matching Tests

**Unit/Integration:**
```
dotnet test test\NuGet.Core.Tests\NuGet.Build.Tasks.Test\NuGet.Build.Tasks.Test.csproj --logger console --verbosity normal
```

**Static-Graph Console (out-of-proc):**
```
dotnet test test\NuGet.Core.Tests\NuGet.Build.Tasks.Console.Test\NuGet.Build.Tasks.Console.Test.csproj --logger console --verbosity normal
```

**Filter by task (e.g., RestoreTask):**
```
dotnet test test\NuGet.Core.Tests\NuGet.Build.Tasks.Test\NuGet.Build.Tasks.Test.csproj --filter "FullyQualifiedName~RestoreTask" --logger console
```

## Build Validation

```
dotnet build src\NuGet.Core\NuGet.Build.Tasks\NuGet.Build.Tasks.csproj
dotnet build src\NuGet.Core\NuGet.Build.Tasks.Console\NuGet.Build.Tasks.Console.csproj
Test-Path "bin\Debug\net472\NuGet.targets" -and Test-Path "bin\Debug\net472\NuGet.RestoreEx.targets"
```
