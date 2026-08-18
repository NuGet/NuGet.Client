# NuGet.Configuration

**Module:** NuGet's hierarchical configuration system (machine-wide → user → project-level NuGet.Config).

## Architecture

- **ISettings:** Public interface for section retrieval, add/update/remove, disk persistence
- **SettingsFile:** Single config file wrapper (XML parse, dirty-track, read-only/machine-wide detection)
- **NuGetConfiguration:** In-memory root element; merges child sections; throws on clear enforcement
- **NuGetPathContext:** Computes global packages folder, fallback folders, HTTP cache from settings
- **PackageSourceProvider:** Manages sources, credentials, package mappings

Settings load from multiple files (machine-wide read-only, user-level editable, project-level). Sections merge until <clear/> encountered. Credentials encrypted with DPAPI (Windows) or ProtectedData (cross-platform).

## High-Risk Invariants

1. **Machine-wide configs read-only:** AddOrUpdate/Remove on machine-wide items throws; changes route to user/project level
2. **Section merging:** Names case-insensitive; <clear/> resets all inherited values in section
3. **Single-load semantics:** Settings loaded once per ISettings instance; external file changes require reload
4. **File discovery:** Windows case-insensitive; Unix tries
   nuget.config → NuGet.config → NuGet.Config

## Build & Test

**Unit tests (Configuration only):**
```cmd
dotnet test test\NuGet.Core.Tests\NuGet.Configuration.Test\NuGet.Configuration.Test.csproj
```

**Filter by class:**
```cmd
dotnet test test\NuGet.Core.Tests\NuGet.Configuration.Test\NuGet.Configuration.Test.csproj --filter "FullyQualifiedName~SettingsTests"
```

**Full module build:**
```cmd
.\build.ps1 -f
```

## Matching Tests

- **SettingsTests.cs:** Hierarchy merging, clear semantics
- **PackageSourceProviderTests.cs:** Source/credential lifecycle
- **SettingsUtilityTests.cs:** Path resolution
- **NuGetPathContextTests.cs:** Folder computation
- **ClientCertificateProviderTests.cs:** Certificate storage/retrieval

## InternalsVisibleTo

NuGet.Configuration.Test, NuGet.Commands.Test, Test.Utility, DynamicProxyGenAssembly2
