# NuGet.Localization

**Scope**: `src\NuGet.Core\NuGet.Localization\` — orchestrates localized satellite assemblies (*.resources.dll) from 13 NuGet.Core projects into a single shipping NuGet package.

## High-Risk Invariants

- **No source code**: Project contains zero .cs files; only MSBuild targets that aggregate and copy pre-built .resources.dll files.
- **MoveLocalizedFilesToLocalizedArtifacts target** (runs BeforePack): Invokes GetNetCoreLocalizedFilesInProjectOutputPath on @(CoreProjects). Excluded projects hardcoded in csproj (NuGet.Build.Tasks.Pack, Microsoft.Build.NuGetSdkResolver, NuGet.Packaging.Core); changes to exclusion list or CoreProjects definition require re-verification of package contents.
- **LocalizationOutputDirectory** = `$(ArtifactsDirectory)LocalizedFiles/`: If OutputPath conventions change in CoreProjects, satellite DLL discovery pattern `$(OutputPath)$(LatestNETCoreTargetFramework)/**/*.resources.dll` fails silently (no error, missing localizations in package).
- **Single TargetFramework**: net10.0 only (LatestNETCoreTargetFramework); multi-framework satellite DLL structure (netstandard2.0, net8.0) collected from CoreProjects' build outputs.

## Validation

Build and verify satellite assembly collection:
```powershell
# From repo root
.\build.ps1 -Configuration Release -Pack

# Verify collection succeeded (check artifact directory)
Get-ChildItem artifacts\LocalizedFiles -Filter "*.resources.dll" -Recurse | Measure-Object

# Verify package includes localizations
dotnet nuget locals global-packages --clear
dotnet add package NuGet.Localization -v <version> --package-directory artifacts\test-pkg
Get-ChildItem artifacts\test-pkg\nuget.localization\*\lib -Filter "*.resources.dll" -Recurse | Measure-Object
```

## No Tests

No dedicated test project. Validation relies on post-build artifact inspection and package structure. If CoreProjects fail to generate .resources.dll files, NuGet.Localization.nupkg ships empty (lib/ contains no satellite assemblies).

## Changes Requiring Re-Verification

- Modification to build/common.project.props CoreProjects ItemGroup definition.
- Changes to excluded projects list in NuGet.Localization.csproj.
- Changes to OutputPath conventions in CoreProjects (e.g., AppendTargetFrameworkToOutputPath).
- Updates to LatestNETCoreTargetFramework in build/common.project.props.

---

**Uncertainty**: Reason for three specific CoreProjects exclusions not documented in csproj; rationale unclear without project-specific flags review.
