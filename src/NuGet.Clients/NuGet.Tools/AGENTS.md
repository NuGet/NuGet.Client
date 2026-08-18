# NuGet.Tools

**Location:** src\NuGet.Clients\NuGet.Tools

VS Extension package implementing core NuGet UI shell, menu commands, and brokered services.

## Build & Test

### Validate Build
\\\cmd
dotnet build src\NuGet.Clients\NuGet.Tools\NuGet.Tools.csproj -c Release
\\\

### Run Unit Tests
\\\cmd
dotnet test test\NuGet.Clients.Tests\NuGet.Tools.Test\NuGet.Tools.Test.csproj -c Release --filter "Category!=Integration" --logger "console;verbosity=normal"
\\\

## Architecture

- **Framework:** net472 (VS 2019+)
- **Type:** AsyncPackage with VSIX embedding (IncludeInVSIX=true, CreateVsixContainer=false)
- **Package GUID:** 5fcc8577-4feb-4d04-ad72-d6c629b083cc

## Critical Invariants

1. **Registry Location:** Experimental hive only (VSSDKTargetPlatformRegRootSuffix=Exp)
   - Standard registry will NOT contain entries; dev/test instances must use /Exp suffix
   - Cannot self-register into production hive

2. **VSIX Container Hosting:** This assembly is **not** a standalone VSIX
   - Consumed by parent package; CreateVsixContainer=false → no .vsix file generated
   - Entry point: NuGetPackage class with 7 brokered services

3. **Localization Lock:** 39 XLF files (13 languages) + VSCT/Resx auto-generation
   - MSBuild target AssignEnCultureToNeutralCto mangles CTO culture metadata
   - Manual XLF edits will be overwritten on rebuild

4. **Copilot Mcp Integration:** CopilotToolInvocationService gates MCP server access
   - Tests mock CopilotMcpFunctionDescriptor; production requires Copilot runtime

## Dependencies

- Microsoft.VisualStudio.Sdk, Microsoft.VSSDK.BuildTools (auto-generated .pkgdef)
- NuGet.Console, NuGet.VisualStudio.Implementation, NuGet.VisualStudio.Interop (for .pkgdef codebases)
- Microsoft.VisualStudio.Copilot (test: Xunit + VS SDK Test Framework)

## High-Risk Changes

- **Guids.cs:** Any GUID change breaks registry binding and menu routing
- **NuGetPackage attributes:** ProvideBrokeredService/ProvideUIContextRule changes affect autoload behavior
- **VSCT structure:** Menu parent GUIDs (guidNuGetPkgString) must match package GUID
