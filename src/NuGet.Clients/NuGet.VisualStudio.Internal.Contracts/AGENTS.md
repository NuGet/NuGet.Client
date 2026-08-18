# NuGet.VisualStudio.Internal.Contracts

MessagePack RPC contract layer for VS IDE package management service communication.

## Architecture

**Core Services** (7 interfaces via ServiceMoniker registry):
- ProjectManagerService (v1.0.0) – install/remove packages, resolve dependencies
- SolutionManagerService (v1.0.0) – solution-level project events
- SearchService (v1.0.0) – package search and metadata
- SourcesService – package source operations
- PackageFileService (v1.0.0) – NuGet package file access
- ProjectUpgraderService (v1.0.0) – project format upgrades
- ProjectManagerServiceState – service lifecycle

**RPC Transport**:
- NuGetServiceMessagePackRpcDescriptor – 29 custom MessagePack formatters
- NuGetJsonRpc – error deserialization (RemoteErrorCode = -31999)
- MessageDelimiters.BigEndianInt32LengthHeader framing

**Data Layer**: 13 ContextInfo DTOs + Formatters for Package/Version/Search/Project metadata

**Error Model**: RemoteError wrapper (TypeName + ILogMessage) serialized via custom code -31999

## High-Risk Invariants

1. **Formatter Registration Audit**: All new formatters must be added to CreateMessagePackFormatters() in NuGetServiceMessagePackRpcDescriptor.cs. Missing registrations break RPC serialization.
2. **Service Version Stability**: Version strings in `NuGetServices.cs` must remain synchronized across the codebase. Mismatches break service discovery.
3. **ContextInfo Struct Compatibility**: Changes to ContextInfo classes require corresponding formatter updates; formatters use hardcoded property names (e.g., "id", "version").

## Validation

`powershell
# Build contracts library
dotnet build src\NuGet.Clients\NuGet.VisualStudio.Internal.Contracts\NuGet.VisualStudio.Internal.Contracts.csproj -c Debug

# Run full test suite (formatter registration audit + round-trip tests)
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Internal.Contracts.Test\ -c Debug --logger "console;verbosity=normal"

# Audit: Verify all formatters registered
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Internal.Contracts.Test\NuGet.VisualStudio.Internal.Contracts.Test.csproj -c Debug --filter "FullyQualifiedName~CreateMessagePackFormatters_Always_RegistersAllFormatters"
`

## Test Coverage

- NuGetServiceMessagePackRpcDescriptorTests – formatter completeness audit
- FormatterTests (29 subclasses) – serialize/deserialize round-trip validation
- ProjectActionTests, ImplicitProjectActionTests – action model correctness
- NuGetJsonRpcTests – RemoteError code routing

## Dependencies & InternalsVisibleTo

- Microsoft.VisualStudio.Sdk, MessagePack (vulnerable versions managed)
- Shared: HashCodeCombiner, NoAllocEnumerateExtensions
- Test access: NuGet.PackageManagement.VisualStudio.Test, NuGet.PackageManagement.UI.Test
