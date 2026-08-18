# NuGet.VisualStudio.Implementation

## Scope
MEF-composed Visual Studio extensibility layer providing NuGet APIs to third-party extensions.
Located: src\NuGet.Clients\NuGet.VisualStudio.Implementation\
Tests: 	est/NuGet.Clients.Tests/NuGet.VisualStudio.Implementation.Test/

## Architecture
- **Platform**: Windows + Visual Studio only (net472, ExcludeFromDotNetBuild=true)
- **Composition**: MEF via [Export] attributes (~15 exports: IVsPackageInstaller, IVsPackageUninstaller, etc.)
- **Async Model**: ServiceBroker RPC with AsyncLazy<IServiceBroker> + JoinableTaskFactory
- **DTE Access**: Restricted to UI thread via JoinableTaskFactory; PumpingJTF bypass only for PowerShell pipeline edge case

## High-Risk Invariants
1. **UI Thread Safety**: All DTE access must flow through JoinableTaskFactory or PumpingJTF
2. **[Export] Contracts**: Extensibility APIs are binary-versioned; changing signatures breaks extensions
3. **ServiceBroker RPC Pattern**: Async-only; no synchronous blocking calls on UI thread
4. **No Cross-Platform**: net472 desktop-only; do not add .NET Core or IPC servers

## Build & Test
`powershell
# Build (net472 desktop only)
dotnet build src\NuGet.Clients\NuGet.VisualStudio.Implementation\NuGet.VisualStudio.Implementation.csproj -c Release --no-restore

# Unit tests
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Implementation.Test\NuGet.VisualStudio.Implementation.Test.csproj --logger "console;verbosity=minimal"

# Test specific class
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Implementation.Test\NuGet.VisualStudio.Implementation.Test.csproj --filter "FullyQualifiedName~YourTestClass"
`
