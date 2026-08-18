# NuGet.Credentials

## Architecture
Core credential provider framework orchestrating HTTP authentication for NuGet clients.

**Key Components:**
- ICredentialProvider interface: async URI-based credential acquisition (all implementations)
- CredentialService: orchestrator with per-provider semaphore, retry cache, interactivity flags
- **Built-in Providers:**
  - DefaultNetworkCredentialsCredentialProvider — .NET default credentials (skipped on retry)
  - PluginCredentialProvider — spawns CLI process, JSON request/response via stdio, timeout default 300s
  - SecurePluginCredentialProvider — managed plugin protocol via NuGet.Protocol.Plugins
- **Plugin Protocol:** Sync JSON via stdin/stdout; exit codes: 0=Success, 1=NotApplicable, 2=Failure

## Platform Invariants
- **Frameworks:** net472 + net8.0 (conditional IS_DESKTOP in PluginException for BinaryFormatter)
- **Process Execution:** Windows-only ProcessWindowStyle.Hidden guarded by IS_DESKTOP; all platforms use stdio JSON
- **Serialization:** PluginException/ProviderException [Serializable] for .NET Framework only; net8.0 omits binary serialization
- **Async Contract:** All GetAsync methods return Task<CredentialResponse> with CancellationToken

## High-Risk Changes
- Plugin process timeout mechanism (CredentialsConstants.ProviderTimeoutSecondsDefault = 300s, env NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS)
- ICredentialProvider semaphore concurrency (static per-process)
- JSON schema for plugin request/response (PluginCredentialRequest, PluginCredentialResponse)
- IS_DESKTOP conditional compilation paths (ProcessWindowStyle visibility, BinaryFormatter)

## Validation

**Build:**
```powershell
dotnet build src\NuGet.Core\NuGet.Credentials\NuGet.Credentials.csproj -c Release
```

**API Surface:**
- Public API baselines are framework-specific under `PublicAPI\net472` and
  `PublicAPI\net8.0`. Update the appropriate `PublicAPI.Unshipped.txt` when the
  public surface changes.

**Unit Tests (10 test classes):**
```powershell
dotnet test test\NuGet.Core.Tests\NuGet.Credentials.Test\NuGet.Credentials.Test.csproj -c Release
dotnet test test\NuGet.Core.Tests\NuGet.Credentials.Test\NuGet.Credentials.Test.csproj -c Release --filter "FullyQualifiedName~PluginCredentialProviderTests"
```
