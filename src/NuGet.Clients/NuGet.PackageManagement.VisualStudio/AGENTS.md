# AGENTS.md: NuGet.PackageManagement.VisualStudio

**Scope**: VS integration layer (IDEs, project systems, restore, threading, credentials)

## Architecture

- **Target**: net472 (VS 2019+) + net8.0
- **Core Exports**: VSSolutionManager, VSPackageRestoreManager, DefaultProjectThreadingService (MEF/[Export])
- **Project Systems**: 11 variants (MSBuild, C++, F#, JS, WebSite, WiX, CPS, Native)
- **UI Threading**: JoinableTaskFactory + ReentrantSemaphore + ThreadHelper.ThrowIfNotOnUIThread()
- **Test**: 40+ XUnit tests (net472 + net8.0) in parallel test project

## High-Risk Invariants

1. **Solution Events → UI Thread**: OnSolutionOpened/Closed/NuGetProjectAdded must call ThreadHelper.ThrowIfNotOnUIThread()
2. **Restore Async off-UI**: VSPackageRestoreManager.cs uses TaskScheduler.Default to unblock UI
3. **MEF Composition**: All service exports must have [Export] + [ImportingConstructor]
4. **Threading Context**: NuGetUIThreadHelper.JoinableTaskFactory is single shared context
5. **Restore Orchestration**: ISolutionManager events trigger RaisePackagesMissingEventForSolutionAsync()

## Validation Commands

```powershell
# Build both frameworks
dotnet build src\NuGet.Clients\NuGet.PackageManagement.VisualStudio\NuGet.PackageManagement.VisualStudio.csproj -c Release

# Run all tests
dotnet test test\NuGet.Clients.Tests\NuGet.PackageManagement.VisualStudio.Test\NuGet.PackageManagement.VisualStudio.Test.csproj -c Release

# Verify MEF exports (expect 15+)
Select-String -Path "src\NuGet.Clients\NuGet.PackageManagement.VisualStudio\**/*.cs" -Pattern "\[Export" | Measure-Object

# Verify UI thread guards (expect 6+)
Select-String -Path "src\NuGet.Clients\NuGet.PackageManagement.VisualStudio\**/*.cs" -Pattern "ThreadHelper.ThrowIfNotOnUIThread"
```

## Ownership Scope

- ✓ IDE layer + project systems + threading + restore events
- ✓ Test assembly + localization (Strings.resx + xlf/)
- ✗ NuGet.VisualStudio (interfaces), NuGet.Indexing, build infrastructure
