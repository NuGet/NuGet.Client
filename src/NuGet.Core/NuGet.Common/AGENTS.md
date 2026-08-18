# NuGet.Common

Foundational library providing logging, concurrency utilities, cryptography, telemetry, and activity correlation for all NuGet libraries. Multi-target (net5.0, net6.0, netstandard2.0); included in VSIX.

## High-Risk Invariants

### File Locking & Platform Behavior
- `ConcurrencyUtilities.ExecuteWithFileLocked()` uses OS-specific file locks via `FileStream` with 3000 retry attempts (~30s) on `UnauthorizedAccessException`
- `FileOptions.DeleteOnClose` disabled by default on Mac/Linux (OS concurrency bugs fixed only in .NET 7); opt-in via `NUGET_ConcurrencyUtils_DeleteOnClose=1`
- Lock file path uses SHA256 hash (UTF32 encoding, truncated to 20 bytes) for case-insensitive path normalization
- `KeyedLock` dictionary cleanup has potential memory leak if `EnterAsync` WaitAsync fails after counter increment

### Static State Reset: BuildEnded Event
- `StaticState.BuildEnded` event fires once when MSBuild-driven build ends; handlers must **invalidate caches, not recompute** (environment not yet updated for next build)
- Handlers must **not swap resources with in-flight work** (no disposing live semaphores, child processes, open writers)
- Subscribers: `ConcurrencyUtilities.ResetEnvironmentCaches()` nulls `_useDeleteOnClose` and `_basePath`
- Critical for MSBuild Server and multithreaded MSBuild scenarios where process is reused

### Cryptography Defaults
- `CryptoHashProvider` defaults to SHA512 silently if initialized with null/empty algorithm string; only SHA256 and SHA512 are valid (case-insensitive)
- Invalid algorithm throws `ArgumentException`

### Activity Correlation: Platform Conditional
- `ActivityCorrelationId` uses `AsyncLocal<string>` on CoreCLR; `CallContext.LogicalSetData/GetData` on .NET Framework
- Ambient GUID-based correlation for cross-layer tracing

## Validation

```powershell
# Concurrency stress test: 50 threads, 3000 retry attempts
dotnet test test\NuGet.Core.Tests\NuGet.Common.Test\NuGet.Common.Test.csproj --filter "ConcurrencyUtilitiesTests"

# StaticState.BuildEnded reset behavior
dotnet test test\NuGet.Core.Tests\NuGet.Common.Test\NuGet.Common.Test.csproj --filter "StaticStateTests"

# File locking under contention
dotnet test test\NuGet.Core.Tests\NuGet.Common.Test\NuGet.Common.Test.csproj --filter "SynchronizationTest"

# Crypto algorithm validation
dotnet test test\NuGet.Core.Tests\NuGet.Common.Test\NuGet.Common.Test.csproj --filter "CryptoHashProviderTest"

# API surface stability
dotnet build src\NuGet.Core\NuGet.Common\NuGet.Common.csproj -c Release

# Verify no unexpected PublicAPI.Unshipped exports (contains only StaticState.BuildEnded and RaiseBuildEnded)
Get-Content src\NuGet.Core\NuGet.Common\PublicAPI.Unshipped.txt | Measure-Object -Line
```

## Dependencies
- `NuGet.Frameworks` (project reference)
- Shared compile items: `EncodingUtility.cs`, `StringBuilderPool.cs`, `TaskResult.cs` (changes affect all consumers)
- `InternalsVisibleTo: NuGet.Common.Test`
