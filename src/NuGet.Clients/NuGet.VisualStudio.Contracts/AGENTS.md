# NuGet.VisualStudio.Contracts

## Scope
RPC contract library for NuGet's Visual Studio Service Broker extensibility. Defines `INuGetProjectService` for async project package queries. Immutable public API (PublicAPI.Shipped.txt); sealed classes use factory construction only.

## Architecture
- **Serialization:** MessagePack with `[Key(n)]` attributes (indices 0–4 immutable for binary stability)
- **Service Moniker:** "Microsoft.VisualStudio.NuGet.NuGetProjectService" v1.0
- **RPC Delimiters:** BigEndianInt32LengthHeader
- **Assembly Version:** Pinned to `$(NuGetSdkVsSemanticVersion).0` (no patch changes) to minimize binding redirects
- **Package Version:** Tracks VS version via `$(NuGetSdkVsSemanticVersion)$(PreReleaseInformationVersion)`

## High-Risk Invariants
1. **MessagePack Key Stability:** Never reorder or remove `[Key(n)]` from `NuGetInstalledPackage`, `InstalledPackagesResult`; new fields only at n+1.
2. **Sealed Contract Classes:** Do not make classes unsealed or change constructors; factory methods (`NuGetContractsFactory`) are the only public creation path.
3. **Interface Non-Implementability:** `INuGetProjectService` marked "should not be implemented"; new methods acceptable, breaking changes prohibited.
4. **Assembly Version Lock:** Do not change AssemblyVersion during patch releases; breaks consumer binding redirects.

## Test & Validate

Build and verify serialization:
```powershell
cd src\NuGet.Clients\NuGet.VisualStudio.Contracts
dotnet build NuGet.VisualStudio.Contracts.csproj
```

Run contract serialization tests:
```powershell
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Implementation.Test\NuGet.VisualStudio.Implementation.Test.csproj --filter "FullyQualifiedName~NuGetProjectServiceV1ContractTests.GetInstalledPackagesAsync_Serialization_Succeeds" -v minimal
```

Verify PublicAPI unchanged (no unshipped entries):
```powershell
Get-Content src\NuGet.Clients\NuGet.VisualStudio.Contracts\PublicAPI.Unshipped.txt | Where-Object { $_ -match '^[A-Z]' } | Measure-Object -Line
```
Expected: 0 non-comment lines.
