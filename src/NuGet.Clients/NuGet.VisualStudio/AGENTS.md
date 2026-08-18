# NuGet.VisualStudio

## Architecture

Public APIs for invoking NuGet services in Visual Studio via MEF (Managed Extensibility Framework).
- **26 IVs* extensibility interfaces** in Extensibility/ and SolutionRestoreManager/
- **Legacy services** (NuGet 5.11 and earlier compatibility)
- **Framework:** net472 (desktop framework only)
- **Package:** NuGet.VisualStudio (shipped as IncludeInVSIX=true)
- **Dependencies:** System.ComponentModel.Composition, Microsoft.VisualStudio.ComponentModelHost

## High-Risk Invariants

1. **Threading Model:** IVsPackageInstaller.InstallPackage() permits background thread invocation if UI thread not blocked. Individual interface remarks must be reviewed for thread-safety guarantees.

2. **COM Interop:** Guid 228F7591-2777-47D7-B81D-FEADFC71CEB5 (ComVisible=false). Assembly version pinned to semantic version to minimize binding redirects.

3. **Public API Stability:** Tracked in PublicAPI.Shipped.txt. New APIs must be added to PublicAPI.Unshipped.txt before merge.

## Validation Commands

**Build:**
```powershell
dotnet build src\NuGet.Clients\NuGet.VisualStudio\NuGet.VisualStudio.csproj -c Release -p:TargetFramework=net472
```

**Unit tests:**
```powershell
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Test\NuGet.VisualStudio.Test.csproj --filter "IVsPathContext"
```
