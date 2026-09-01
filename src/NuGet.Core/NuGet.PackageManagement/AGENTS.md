# NuGet.PackageManagement AGENTS.md

## Module Scope
Core orchestration for package install/uninstall/restore operations across project types (MSBuild, PackagesConfig, BuildIntegrated). Contracts with NuGet.Commands and NuGet.Resolver; ships in VSIX.

## High-Risk Invariants

**1. Project Type Polymorphism + Async Lifecycle**
- `NuGetProject` abstract base enforces `InstallPackageAsync()` / `UninstallPackageAsync()` per type. PreProcess/PostProcess hooks run before batch—shared caching across concurrent calls risks race conditions. Uninstall always sets SourceRepository=null; Install requires non-null.
- Test: `dotnet test test\NuGet.Core.Tests\NuGet.PackageManagement.Test\NuGet.PackageManagement.Test.csproj --filter "FullyQualifiedName~NuGetPackageManagerTests.InstallPackageTests&FullyQualifiedName~MultipleThreads"`

**2. GatherCache + SourceCacheContext Mutability**
- `ResolutionContext` holds both; reuse without clear() causes stale version-graph bias in offline/retry flows. GatherCache requires non-null guard.
- Test: `dotnet test test\NuGet.Core.Tests\NuGet.PackageManagement.Test\NuGet.PackageManagement.Test.csproj --filter "FullyQualifiedName~GatherCache"`

**3. Compatibility Gate Post-Download**
- `IInstallationCompatibility.EnsurePackageCompatibility()` validates after PreFetcher succeeds. Partial file state on failure requires FileModifiers cleanup.
- Test: `dotnet test test\NuGet.Core.Tests\NuGet.PackageManagement.Test\NuGet.PackageManagement.Test.csproj --filter "FullyQualifiedName~InstallationCompatibility"`

**4. InternalsVisibleTo Leakage**
- Exposes internals to 3 test assemblies. Internal API changes break test contracts silently.
- Validate: PublicAPI.Shipped.txt must track all public surface changes.

**5. OperationId Batching (No Idempotency)**
- Guid-based tracing per BatchStart/BatchEnd events. Duplicates across concurrent calls cause telemetry collision.
- Test: `dotnet test test\NuGet.Core.Tests\NuGet.PackageManagement.Test\NuGet.PackageManagement.Test.csproj --filter "FullyQualifiedName~BatchedEventTests"`

## Build & Validation
```cmd
dotnet build src\NuGet.Core\NuGet.PackageManagement\NuGet.PackageManagement.csproj /p:TreatWarningsAsErrors=true
dotnet test test\NuGet.Core.Tests\NuGet.PackageManagement.Test\NuGet.PackageManagement.Test.csproj
```
