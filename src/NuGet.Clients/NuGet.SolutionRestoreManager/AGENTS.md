# NuGet.SolutionRestoreManager

## Architecture

VS solution restore orchestration bridging MEF (VsSolutionRestoreService),
build events (RestoreManagerPackage), and restore pipeline via
IVsSolutionRestoreService* contracts (versions 1–5, single sealed export).
Shared MEF parts coordinate scheduling; NonShared jobs execute per-restore.

**Platform:** net472 desktop-only (VSSDK ImportVSSDKTargets=true).

## High-Risk Invariants

1. **Threading Model:** JoinableTaskFactory dispatch required for UI thread callbacks.
   AsyncLazy<IVsSolution2> guards VS COM service access. CancellationToken
   propagates through all async boundaries (VsSolutionRestoreService →
   SolutionRestoreWorker → SolutionRestoreJob).

2. **Restore Scheduling:** BlockingCollection queue capped at 150 requests.
   Idle timeout: 400ms. Bulk restore coordination timeout: 5 minutes.
   Exceeding queue limit blocks nominators.

3. **MEF Isolation:** VsSolutionRestoreService (Shared) + SolutionRestoreWorker (Shared)
   remain persistent; SolutionRestoreJob (NonShared) created per-restore invocation.
   InternalsVisibleTo restricted to test assembly only.

4. **Backward Compatibility:** Five contract versions exported from single class.
   Removing or renaming any IVsSolutionRestoreService* breaks 3rd-party VS extensions.

## Validation

Build:
  dotnet msbuild src\NuGet.Clients\NuGet.SolutionRestoreManager\NuGet.SolutionRestoreManager.csproj

Test (xUnit; requires VS runtime):
  dotnet test test\NuGet.Clients.Tests\NuGet.SolutionRestoreManager.Test\NuGet.SolutionRestoreManager.Test.csproj --filter "FullyQualifiedName!~ErrorListFixers"

Contract exports:
  dotnet msbuild src\NuGet.Clients\NuGet.SolutionRestoreManager\NuGet.SolutionRestoreManager.csproj /t:GeneratePkgDef

## Cross-Cutting Concerns

- **Dependencies:** NuGet.PackageManagement, NuGet.PackageManagement.VisualStudio,
  NuGet.VisualStudio.Common, Microsoft.VisualStudio.Sdk
- **Shared Build:** Directory.Build.targets imports VSSDK targets via
  PkgMicrosoft_VSSDK_BuildTools.
- **Test Utilities:** DispatcherThreadCollection, ErrorListEntryTestUtility
  (indicate mock-heavy threading tests).
