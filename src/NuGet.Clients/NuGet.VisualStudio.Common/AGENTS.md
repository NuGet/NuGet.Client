# AGENTS.md: NuGet.VisualStudio.Common

**Scope:** `src\NuGet.Clients\NuGet.VisualStudio.Common`

## Architecture
- **Shipping VSIX component** (`<Shipping>true</Shipping>`, `<IncludeInVsix>true</IncludeInVsix>`)
- **Framework:** .NET 4.x (NETFXTargetFramework, per csproj:3)
- **API surface:** 15+ public interfaces (IVisualStudioShell, INuGetUILogger, INuGetErrorList, IVsProjectAdapter)
- **13 subdirectories:** IDE, Services, Telemetry, ProjectSystems, SolutionRestore, Console, Etw, Events, Experimentation, Runtime, SourceControl, UserInterfaceService, xlf

## High-Risk Invariants
**Threading:** JoinableTaskFactory main-thread enforcement required
- NuGetUIThreadHelper.cs: Lazy initialization with nullable fallback
- VisualStudioShell.cs: All async VS operations call `SwitchToMainThreadAsync()`
- OutputConsoleLogger.cs: ReentrantSemaphore with JoinableTaskFactory.Context
- **Risk:** Null exception if VsTaskLibraryHelper.ServiceInstance unavailable during MEF composition

**MEF Composition:**
- OutputConsoleLogger.cs: [Export], [PartCreationPolicy(Shared)], ImportingConstructor with AsyncServiceProvider fallback
- OutputConsoleLogger.cs: InternalsVisibleTo NuGet.PackageManagement.VisualStudio.Test, NuGet.VisualStudio.Common.Test
- **Risk:** Deadlock if service initialization order reversed

**Dependencies:**
- ProjectReferences (csproj:27-29): NuGet.PackageManagement, NuGet.VisualStudio.Internal.Contracts, NuGet.VisualStudio
- Framework: Microsoft.VisualStudio.Sdk, ComponentModelHost, ProjectSystem, MEF

## Matching Tests
- `test\NuGet.Clients.Tests\NuGet.VisualStudio.Common.Test` (23 test files)
- OutputConsoleLoggerTests: Mocks IVisualStudioShell, IOutputConsole, tests ReentrantSemaphore behavior
- ErrorListTableDataSourceTests: MEF composition validation
- NuGetFeatureFlagServiceTests, ExperimentationServiceTests: Service abstractions

## Validation Commands (Windows)
```powershell
# Verify ownership scope
(Get-ChildItem src\NuGet.Clients\NuGet.VisualStudio.Common -Filter "*.cs" -Recurse).Count
# Expected: 105

# Build component
dotnet build src\NuGet.Clients\NuGet.VisualStudio.Common\NuGet.VisualStudio.Common.csproj -c Release

# Run matching tests
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Common.Test\NuGet.VisualStudio.Common.Test.csproj --filter Category!=Integration -v minimal

# Verify threading invariants
(Select-String -Path src\NuGet.Clients\NuGet.VisualStudio.Common\OutputConsoleLogger.cs -Pattern "SwitchToMainThreadAsync|ReentrantSemaphore").Count
# Expected: 13+

# Check MEF exports
(Select-String -Path src\NuGet.Clients\NuGet.VisualStudio.Common -Filter "*.cs" -Recurse -Pattern "\[Export\]").Count
# Expected: 5+
```
