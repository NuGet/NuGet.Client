# NuGet.Commands

## Ownership
- **Path:** `src\NuGet.Core\NuGet.Commands\`
- **Scope:** Command implementations (restore, pack, list, verify, sign, trusted-signers, client-certs)
- **Frameworks:** net472, net8.0 (XPLAT, public API tracked per-framework)
- **Dependencies:** NuGet.ProjectModel, NuGet.Credentials, NuGet.Protocol, NuGet.DependencyResolver

## High-Risk Invariants

### 1. Restore Graph Determinism & No-Op Cache
- **Location:** `RestoreCommand.cs` (EvaluateNoOpAsync), `LockFileBuilderCache.cs` (ConcurrentDictionary caches)
- **Risk:** Cache invalidation logic must detect dgspec changes; stale cache causes wrong restore results
- **Test:** `dotnet test test\NuGet.Core.Tests\NuGet.Commands.Test --filter "NoOp" -c Release`

### 2. Concurrency: Multi-Project Restore
- **Location:** `RestoreCommandProvidersCache.cs` (4× ConcurrentDictionary), `LockFileBuilderCache.cs` (Lazy<T> guards)
- **Risk:** Provider cache GetOrAdd must not duplicate IRemoteDependencyProvider instances; Lazy wrapping prevents double-work
- **Test:** `dotnet test test\NuGet.Core.Tests\NuGet.Commands.Test --filter "RestoreCommandProviders" -c Release`

### 3. Lock-File Stability (packages.lock.json)
- **Location:** `PackagesLockFileBuilder.cs` (direct/transitive/central-version classification), `RestoreCommand.cs` (lock-file evaluation)
- **Risk:** Central package version changes must invalidate lock-file; transitive pinning logic couples to project model
- **Test:** `dotnet test test\NuGet.Core.Tests\NuGet.Commands.Test --filter "PackagesLockFile" -c Release`

### 4. Telemetry Instrumentation
- **Location:** `CommandsEventSource.cs` (9 event tasks, ETW keywords), `RestoreCommand.cs` (70+ property names)
- **Risk:** Lost telemetry events mask restore failures; audit feature telemetry spans external vulnerability provider
- **Test:** `dotnet build src\NuGet.Core\NuGet.Commands\NuGet.Commands.csproj /p:TreatWarningsAsErrors=true`

## Test Coverage
- **Unit:** `test\NuGet.Core.Tests\NuGet.Commands.Test\` (8 internals-visible dependencies)
- **Functional:** `test\NuGet.Core.FuncTests\NuGet.Commands.FuncTest\` (integration with asset/cache output)

## Validation
```powershell
dotnet build src\NuGet.Core\NuGet.Commands\NuGet.Commands.csproj /p:TreatWarningsAsErrors=true
dotnet test test\NuGet.Core.Tests\NuGet.Commands.Test\NuGet.Commands.Test.csproj --filter "FullyQualifiedName~Restore|FullyQualifiedName~Pack|FullyQualifiedName~LockFile" -c Release
```
