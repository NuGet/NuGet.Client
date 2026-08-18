# AGENTS.md: NuGet.CommandLine.XPlat

**Scope**: `src\NuGet.Core\NuGet.CommandLine.XPlat` (net10.0 executable)

## Architecture & High-Risk Invariants

- **Dual-mode CLI**: Routes `dotnet nuget package <cmd>` (DEBUG-only) vs `dotnet nuget <cmd>` (production) via Program.cs conditional
- **Command registration**: All commands implement static `Register()` method via System.CommandLine; central registration in Program.cs
- **AOT constraint**: `[RequiresUnreferencedCode]` on Program.Run() and MainInternal()—in-process MSBuild reflection loads task assemblies dynamically; cannot trim
- **IVirtualProjectBuilder interface**: SDK injection point for file-based app support (.cs and shebang scripts); invert dependency pattern avoids SDK source-build cycle (IVirtualProjectBuilder.cs)
- **Internals visibility**: Dotnet.Integration.Test, NuGet.CommandLine.Xplat.Tests, NuGet.XPlat.FuncTest, DynamicProxyGenAssembly2 (csproj:58-62)

## Matching Tests

**Unit Tests**: `test\NuGet.Core.Tests\NuGet.CommandLine.Xplat.Tests\`
- CommandLineUtility, MSBuildAPIUtility, XPlat config, ListPackageCommand, PackageSearch

**Functional Tests**: `test\NuGet.Core.FuncTests\NuGet.XPlat.FuncTest\`
- Add/Remove packages, Sign/Verify, Why, Config, Locals, Push, List, Client-cert workflows

## Validation

```powershell
# Build release
dotnet build src\NuGet.Core\NuGet.CommandLine.XPlat\NuGet.CommandLine.XPlat.csproj -c Release

# Run unit tests
dotnet test test\NuGet.Core.Tests\NuGet.CommandLine.Xplat.Tests\NuGet.CommandLine.Xplat.Tests.csproj

# Run functional tests
dotnet test test\NuGet.Core.FuncTests\NuGet.XPlat.FuncTest\NuGet.XPlat.FuncTest.csproj

# AOT trim surface check
dotnet publish src\NuGet.Core\NuGet.CommandLine.XPlat\ -c Release -p:PublishTrimmed=true -p:TrimMode=partial 2>&1 | Select-String "warning IL"
```
