# NuGet.CommandLine Agent Scope

## Architecture

NuGet.CommandLine is a single Windows desktop executable (`NuGet.exe`) using MEF for command composition and ILRepack for binary merging. Commands are discovered via `[Import]`/`[ImportMany]` attributes (CommandManager.cs). Extensions load from `%LocalAppData%\NuGet\{Commands,CredentialProviders}` or paths set via environment variables `NUGET_EXTENSIONS_PATH` and `NUGET_CREDENTIALPROVIDERS_PATH`.

**Supported Windows versions:** Windows 7, 8, 8.1, 10 (app.manifest).
**Build target:** .NET Framework 4.6.2+.
**Output:** ILRepack-merged executable with embedded localized satellites (13 languages).

## High-Risk Invariants

1. **ILRepack determinism**: Binary merge order is significant; NuGet.Core.dll must be last (ilmerge.props, csproj). Test: `dotnet build src\NuGet.Clients\NuGet.CommandLine\NuGet.CommandLine.csproj`
2. **Localization satellites**: Embedded resource DLLs only in CI builds (csproj); local debug builds skip them.
3. **Extension discovery order**: Environment variables checked before default paths; tests must disable extensions via `Program.IgnoreExtensions = true`.
4. **Command lookup**: Case-insensitive prefix matching with ambiguity detection (CommandManager.GetCommand); exact matches preferred.

## Test Execution

**Unit tests:**
```cmd
dotnet test test\NuGet.Clients.Tests\NuGet.CommandLine.Test\NuGet.CommandLine.Test.csproj --filter "Category!=Integration"
```

**Functional tests:**
```cmd
dotnet test test\NuGet.Clients.FuncTests\NuGet.CommandLine.FuncTest\NuGet.CommandLine.FuncTest.csproj
```

**Extension loading tests:** Verify SampleCommandLineExtensions (TestExtensions directory) is copied to test output before running extension-related tests.

## Build

```cmd
dotnet build src\NuGet.Clients\NuGet.CommandLine\NuGet.CommandLine.csproj
```

Produces `bin/Configuration/net462/NuGet.exe` (pre-merge); post-build target `ILMergeNuGetExe` produces `artifacts/NuGet.exe`.
