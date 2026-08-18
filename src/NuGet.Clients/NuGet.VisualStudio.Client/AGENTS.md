# AGENTS.md – NuGet.VisualStudio.Client

## Architecture

**Extension Type**: Microsoft.VisualStudio.Extensibility in-process VSIX
**Target Framework**: net472 (NETFXTargetFramework from build/common.project.props)
**RPC Protocol**: StreamJsonRpc + MessagePack (BigEndianInt32LengthHeader delimiters)
**In-Process**: Required (NuGetExtension.cs RequiresInProcessHosting = true)

## Contracts & Serialization

- **NuGet.VisualStudio.Contracts**: Public IPC surface (INuGetProjectService, etc.)
- **NuGet.VisualStudio.Internal.Contracts**: MessagePack formatters, RemoteErrorCode handling, service descriptors
- **Serializer**: Custom NuGetServiceMessagePackRpcDescriptor with MessagePackSerializerOptions resolver chain
- **Error Handling**: RemoteErrorCode.RemoteError mapped to RemoteError type in NuGetJsonRpc.GetErrorDetailsDataType()

## High-Risk Invariants

1. **VSIX Packaging**: IncludeCopyLocalReferencesInVSIXContainer=true; .vsixinclude/ignore lists enforce what ships
2. **Ngen Compilation**: 15+ core assemblies (NuGet.Protocol, NuGet.PackageManagement, NuGet.Common, etc.) marked Ngen=True with Priority 2-3
3. **No Standalone Assembly**: NuGetExtension.cs is minimal entry point; logic defers to Implementation & Contracts projects
4. **UI Thread**: In-process hosting means all calls execute on VS UI thread; no blocking operations

## Validation Commands

`powershell
# Verify extension configuration
dotnet build src\NuGet.Clients\NuGet.VisualStudio.Client\NuGet.VisualStudio.Client.csproj
  -c Release --no-restore 2>&1 | Select-String "error"

# Test contract serialization
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Internal.Contracts.Test\NuGet.VisualStudio.Internal.Contracts.Test.csproj
  --no-build -c Release --filter "MessagePack" 2>&1 | Measure-Object -Line

# Verify VSIX manifest targets (VS 2022+, x86/amd64)
Select-String '<InstallationTarget.*Version="\[17'
  src\NuGet.Clients\NuGet.VisualStudio.Client\source.extension.vsixmanifest | Measure-Object
`

## Known Gaps

- **Exact NgenArchitecture**: Assumed x64 default; check CI configuration
- **Async Threading Model**: Implementation in NuGet.VisualStudio.Implementation.csproj (not inspected here)
- **Client-Side State**: INuGetProjectManagerServiceState, LoadingStatus in Internal.Contracts; full behavior in Implementation
