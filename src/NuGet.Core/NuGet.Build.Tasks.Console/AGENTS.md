# NuGet.Build.Tasks.Console

**Shipped console executable** for in-process MSBuild static graph restore. Targets `net472` and `net10.0`+.

## Console Protocol & Invariants

**Entry point**: `Program.MainInternal()` (async Task<int>)

**Arguments** (3-4 only; see Program.cs):
1. Semicolon-delimited options: `key1=val1;key2=val2`
2. Full path to MSBuild.exe (file must exist)
3. Full path to project/solution file
4. _(optional)_ Semicolon-delimited global properties OR binary from STDIN (BinaryReader: count as Int32, then key/value string pairs)

**Output**: JSON-serialized to stdout via `ConsoleOutLogMessage.ToJson()`

**Exit codes**: 0=success, 1=failed, -1=unhandled exception

## High-Risk Invariants

- **IL2026**: Application runs MSBuild in-process via reflection; trim-unsafe on .NET. Annotation terminates at Main entry point (Program.cs).
- **Binary STDIN**: Count ≤ short.MaxValue enforced (Program.cs); no schema versioning. Fragile if host protocol changes.
- **AppDomain (NET Framework)**: First invocation runs in default domain, second in child domain (ApplicationBase = MSBuild.exe dir). Binding redirects from MSBuild.exe.config inherited.
- **Feature flags** (Program.cs): `EnableCacheFileEnumerations`, `LoadAllFilesAsReadonly`, `SkipEagerWildcardEvaluations` set before evaluation. Wildcard regex `[*?]+.*(?<!proj)$` prevents DOS.

## Validation Commands

```powershell
# Build
dotnet build src\NuGet.Core\NuGet.Build.Tasks.Console\NuGet.Build.Tasks.Console.csproj -c Release

# Test console protocol & binary deserialization
dotnet test test\NuGet.Core.Tests\NuGet.Build.Tasks.Console.Test\NuGet.Build.Tasks.Console.Test.csproj --filter "ProgramTests"

# Verify AppDomain setup (NET Framework only)
dotnet test test\NuGet.Core.Tests\NuGet.Build.Tasks.Console.Test\NuGet.Build.Tasks.Console.Test.csproj --filter "MSBuildStaticGraphRestore"
```

## Uncertainties

- AppDomain behavior requires Visual Studio MSBuild.exe.config; host environment must match test instance.
- STDIN binary protocol tight coupling; callers must match BinaryWriter format (see test\NuGet.Core.Tests\...\ProgramTests.cs).
