# NuGet.VisualStudio.Interop

**Location**: src\NuGet.Clients\NuGet.VisualStudio.Interop
**Framework**: net472 (via build\common.project.props)
**Shipping**: true; IncludeInVsix: true
**Assembly Version**: Hard-coded 1.0.0.0

## Architecture

Single source file TemplateWizard.cs implements COM interface IWizard (Microsoft.VisualStudio.TemplateWizard), proxying to MEF-composed IVsTemplateWizard via ServiceProvider from DTE automation object.

**Dependencies**:
- Project: NuGet.VisualStudio.csproj
- NuGet: Microsoft.VisualStudio.Shell.15.0, MessagePack, Newtonsoft.Json
- GAC: System.ComponentModel.Composition

## High-Risk Invariants

1. **Net472-only**: Cannot be retargeted to .NET Core; Windows-only, tied to VS 2017+ process
2. **COM interop**: IWizard contract is external; breaking changes require VS side coordination
3. **MEF composition**: Service resolution via DefaultExportProvider at runtime—no compile-time safety
4. **Assembly version lock**: 1.0.0.0 frozen; binding redirects required if consumer expectations change
5. **No isolated tests**: Validation via 	est/NuGet.Clients.Tests/NuGet.VisualStudio.Test (NuGet.VisualStudio.Test.csproj) as indirect dependency

## Build & Validation

`powershell
# Build this project
dotnet build src\NuGet.Clients\NuGet.VisualStudio.Interop\NuGet.VisualStudio.Interop.csproj -c Release

# Run related tests (indirect coverage)
dotnet test test\NuGet.Clients.Tests\NuGet.VisualStudio.Test\NuGet.VisualStudio.Test.csproj --filter "FullyQualifiedName~VisualStudio"

# Validate in solution
dotnet build NuGet-VS.slnf --no-restore -c Release
`

## Known Constraints

- MessagePack vulnerability mitigation via transitive Microsoft.VisualStudio.Shell.15.0 (acknowledged in csproj)
- ThreadHelper.ThrowIfNotOnUIThread() assertions: changes to async patterns will fail at runtime
- No direct unit tests; MEF resolution errors surface only in VS designer/project creation workflows
