# AGENTS.md — NuGet.Protocol

## High-Risk Invariants

**SourceRepository Provider Cache (SourceRepository.cs)**
- Immutable `Dictionary<Type, IReadOnlyList<INuGetResourceProvider>>` sized to 25 resource types.
- Resource acquisition ONLY via `await sourceRepository.GetResourceAsync<T>(cancellationToken)`.
- Lazy-initialized providers in Repository.ProviderFactory.GetCoreV3() (30 yields); position-aware insertion.

**HTTP Cache Concurrency (HttpSource.cs, HttpCacheUtility.cs)**
- File locking mandatory: all cache ops wrapped in `ConcurrencyUtilities.ExecuteWithFileLockedAsync`.
- Atomic pattern: write to .dat-new, then atomic rename to .dat.
- Persistent cache when `HttpSourceCacheContext.MaxAge > TimeSpan.Zero`; temporary when `== Zero` (caller owns cleanup).
- Single `SemaphoreSlim(1,1)` serializes HttpClient recreation—do not spawn multiple HttpSource per source URI.

**Authentication Retry Bounds (HttpSourceAuthenticationHandler.cs, AmbientAuthenticationState.cs)**
- Three-layer handler stack: HttpSourceAuthenticationHandler (MaxAuthRetries=4) → StsAuthenticationHandler → ProxyAuthenticationHandler (3 retries).
- Per-host credential state in `AmbientAuthenticationState`; static `_credentialPromptLock` serializes all prompts.
- Reentrant calls to credential prompt will deadlock—avoid nested auth in validators.

## Validation Commands

```powershell
# Unit tests: authentication, caching, retry logic
dotnet test test\NuGet.Core.Tests\NuGet.Protocol.Tests\NuGet.Protocol.Tests.csproj `
  --filter "HttpSource|HttpRetry|Cache|Authentication" --logger "console;verbosity=detailed"

# Functional tests: end-to-end source + resource flow
dotnet test test\NuGet.Core.FuncTests\NuGet.Protocol.FuncTest\NuGet.Protocol.FuncTest.csproj `
  --filter "RemoteRepository|SourceRepository" --logger "console;verbosity=detailed"

# Build + analyze public API (per-TFM)
dotnet build src\NuGet.Core\NuGet.Protocol\NuGet.Protocol.csproj `
  -p:UsePublicApiAnalyzer=perTfm -p:Configuration=Release

# Full validation (build + unit + func)
.\build.ps1 -f -RunUnitTests
```

## Architecture Notes

- **Copy-compiled sources**: EncodingUtility, StringBuilderPool, TaskResult (in $(SharedDirectory)) linked at compile; prefer over reinvention.
- **Localization**: Strings.resx changes auto-generate Strings.Designer.cs and xlf files; run full build.
- **Target frameworks**: net472, net8.0; System.Text.Json conditional on .NETFramework/.NETStandard.
- **Throttling**: IThrottle injected at HttpSource construction; HttpSourceResourceProvider.Throttle static governs all subsequent creation.
