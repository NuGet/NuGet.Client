# AGENTS.md: NuGet.PackageManagement.UI

**Scope:** `src\NuGet.Clients\NuGet.PackageManagement.UI` — Visual Studio Package Manager UI, tool windows, dialogs, package metadata.

## Architecture

- **PackageManagerControl**: Main WPF UserControl (IVsWindowSearch, IPackageManagerControlViewModel)
- **PackageManagerToolWindowPane**: VS ToolWindowPane host (IVsWindowFrameNotify3)
- **Threading Model**: Mandatory `NuGetUIThreadHelper.JoinableTaskFactory` for all async operations
  - RunAsync() for fire-and-forget background tasks
  - SwitchToMainThreadAsync() for UI marshaling
  - Run() for sync-over-async blocking
- **XAML Layers**: 32 XAML files (controls, dialogs, indicators); theme/brush resources via DynamicResource
- **Accessibility**: Custom AutomationPeers (ButtonHyperlinkAutomationPeer, ToggleableItemAutomationPeer implementing IToggleProvider)
- **Localization**: 13 XLF satellite resources; Resources.resx with PublicResXFileCodeGenerator; T4 template for PackageIconMonikers
- **Target**: .NET Framework (NETFXTargetFramework); requires PresentationFramework, System.Runtime.Caching
- **Tests**: test\NuGet.Clients.Tests\NuGet.PackageManagement.UI.Test with WpfFactAttribute/WpfTheoryAttribute custom xunit discoverers

## High-Risk Invariants

1. **All background operations must use NuGetUIThreadHelper.JoinableTaskFactory**: Failure causes UI thread hangs or race conditions in VS event handlers.
2. **No direct ThreadPool.QueueUserWorkItem or Task.Run without JoinableTaskFactory**: Blocks VS shutdown and breaks modal dialogs.
3. **AutomationPeer implementations must preserve inheritance chain**: Custom peers override CreateItemAutomationPeer() to ensure accessibility tree consistency.
4. **XAML resources must use ImageThemingUtilities for theme colors**: Missing themes break dark/light mode transitions.
5. **Localization is generated from Resources.resx**: Edit the `.resx`, then build the project to regenerate the designer and `.xlf` files.

## Build & Test Commands

```powershell
# Build
dotnet build src\NuGet.Clients\NuGet.PackageManagement.UI\NuGet.PackageManagement.UI.csproj -c Release

# Run all UI tests
dotnet test test\NuGet.Clients.Tests\NuGet.PackageManagement.UI.Test\NuGet.PackageManagement.UI.Test.csproj --no-build

# Run specific WPF test
dotnet test test\NuGet.Clients.Tests\NuGet.PackageManagement.UI.Test\NuGet.PackageManagement.UI.Test.csproj --filter "FullyQualifiedName~PackageItemLoaderTests" --no-build
```
