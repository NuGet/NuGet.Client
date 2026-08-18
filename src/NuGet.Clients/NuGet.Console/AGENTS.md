# NuGet.Console Agent Scoping

## Ownership
`src\NuGet.Clients\NuGet.Console\` — VSIX-bundled PowerShell console UI for Package Manager (WPF, .NET Framework only).

## Architecture Invariants
- **MEF Composition**: Exports `IPowerConsoleWindow`, `IWpfConsoleService`, `IScriptExecutor` via `[Export]` attributes; multi-host via `[ImportMany] IHostProvider`.
- **UI-Thread Gating**: All cross-process calls marshal via `NuGetUIThreadHelper.JoinableTaskFactory` and VS `ThreadHelper.ThrowIfNotOnUIThread()`. Violations cause deadlock/hang.
- **PowerShell 3.x Host**: `ScriptExecutor` wraps init.ps1 execution; async events trigger console prompt refresh. `IPSNuGetProjectContext` bridges PS session state.
- **Output Interop**: `OutputConsole` (non-blocking) and `BuildOutputConsole` write to VS Output pane via `IVsOutputWindow` — thread-safe by AsyncLazy marshaling.
- **Dispatcher Concurrency**: `ConsoleDispatcher` uses `BlockingCollection<VsKeyInfo>` for input buffering; `ConcurrentDictionary` tracks init.ps1 per-package state.

## High-Risk Edits
- Change `AsyncLazy` initialization pattern → UI hangs.
- Remove JoinableTaskFactory marshaling → cross-thread access violation.
- Modify MEF metadata/export names → composition fails silently.
- Add Thread.Sleep or blocking I/O in dispatcher → keyboard unresponsive.

## Matching Tests
- **Unit**: `test\NuGet.Clients.Tests\NuGetConsole.Host.PowerShell.Test\` — cmdlet + runspace tests.
- **Contract**: `test\NuGet.Tests.Apex\NuGet.Console.TestContract\` — APEX integration tests (requires VS).
- **Logger**: `test\NuGet.Clients.Tests\NuGet.VisualStudio.Common.Test\OutputConsoleLoggerTests*.cs` — output pane logging.

## Validation Commands
```
dotnet build src\NuGet.Clients\NuGet.Console\NuGet.Console.csproj
dotnet test test\NuGet.Clients.Tests\NuGetConsole.Host.PowerShell.Test\ --logger=console
dotnet build /p:IncludeInVSIX=true src\NuGet.Clients\NuGet.Console\NuGet.Console.csproj
```

## Dependencies
- `Microsoft.VisualStudio.Sdk`, `Microsoft.PowerShell.3.ReferenceAssemblies`
- `NuGet.PackageManagement.UI`, `NuGet.VisualStudio` (transitive)
- Win-only; requires VS 2015+; no Mono/CoreCLR support.
