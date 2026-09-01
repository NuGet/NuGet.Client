# AGENTS.md: NuGet.PackageManagement.PowerShellCmdlets

**Owns:** Package Manager Console PowerShell host + 9 NuGet cmdlets (Find-Package, Get-Package, Install-Package, Update-Package, Uninstall-Package, Add-BindingRedirect, Get-Project, Sync-Package, Tab Expansion).

**Target Framework:** net472 only (no cross-platform). PowerShell 2.0+ manifest. Visual Studio-bound via DTE automation.

## High-Risk Invariants

1. **Runspace Threading Lock (net472 only):**
   - RunspaceDispatcher.WithLock() uses SemaphoreSlim(1,1) + [ThreadStatic] IHaveTheLock
   - All pipeline invocations (sync/async) must serialize through this lock
   - **Violation:** Direct Runspace.Invoke() outside WithLock = deadlock or race
   - **Validation:** dotnet build src\NuGet.Clients\NuGet.PackageManagement.PowerShellCmdlets\NuGet.PackageManagement.PowerShellCmdlets.csproj -c Release

2. **Satellite Assembly Generation:**
   - 13 XLF files → satellite DLLs via GenerateSatelliteAssembliesForCore=true
   - Missing XLF = missing language pack
   - Build the project to regenerate and validate satellite assemblies.

3. **Manifest Module Update:**
   - Post-build target runs UpdateNuGetModuleManifest.ps1 to patch NuGet.psd1 with runtime FullName
   - Script validates **exactly one** occurrence of 'NuGet.PackageManagement.PowerShellCmdlets.dll' and fails on mismatch
   - **Validation:** Build output captures manifest patching success

4. **UI Thread Barrier:**
   - ThreadHelper.ThrowIfOnUIThread() enforces runspace operations off-UI-thread
   - Cmdlet base class (NuGetPowerShellBaseCommand) must not call UI methods directly
   - **Validation:** Code review + test fixture usage

## Matching Tests

**Project:** `test\NuGet.Clients.Tests\NuGetConsole.Host.PowerShell.Test\`
**Test Commands:**
- dotnet test test\NuGet.Clients.Tests\NuGetConsole.Host.PowerShell.Test\NuGetConsole.Host.PowerShell.Test.csproj -c Release --logger "console;verbosity=detailed"
- Filter by fixture: --filter "GetPackageCommandTests"

**Key Fixtures:**
- CmdletRunspaceFixture (isolated runspace, minimal PSHost)
- GetPackageCommandTests, FindPackageCommandTests (async cmdlet execution)

## Build & Validation

```powershell
# Full build
dotnet build src\NuGet.Clients\NuGet.PackageManagement.PowerShellCmdlets\NuGet.PackageManagement.PowerShellCmdlets.csproj -c Release

# Test coverage
dotnet test test\NuGet.Clients.Tests\NuGetConsole.Host.PowerShell.Test\NuGetConsole.Host.PowerShell.Test.csproj -c Release

# Verify satellite assemblies
Get-ChildItem src\NuGet.Clients\NuGet.PackageManagement.PowerShellCmdlets\bin\Release\net472\*/NuGet.PackageManagement.PowerShellCmdlets.resources.dll
```

## Constraints & Assumptions

- **No async/await in cmdlet implementations:** BlockingCollection + Semaphore enforce sync dispatch
- **Localization immutable post-build:** XLF files frozen; manifest auto-patches only assembly identity
- **Test isolation:** CmdletRunspaceFixture creates fresh runspace per test (no state leakage)
