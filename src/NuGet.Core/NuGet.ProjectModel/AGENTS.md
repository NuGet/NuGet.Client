# NuGet.ProjectModel

Core models for PackageReference-based restore: PackageSpec, DependencyGraphSpec, LockFile (v3/v4), and PackagesLockFile.

## Architecture

**Three-tier design:**
- **Models**: PackageSpec (framework-aware buildable), DependencyGraphSpec (project graph), LockFile (assets file v3/v4), ProjectRestoreMetadata (restore context)
- **Streaming JSON**: Utf8JsonStreamReader (zero-allocation), version-aware converters (Utf8JsonStreamLockFileConverter, JsonPackageSpecReader)
- **Hashing**: FnvHash64 (default, non-cryptographic) with SHA512 fallback via NUGET_ENABLE_LEGACY_DGSPEC_HASH_FUNCTION env var

## High-Risk Invariants

**Hash Function Versioning**: Dgspec cache keyed on hash. Switching env var NUGET_ENABLE_LEGACY_DGSPEC_HASH_FUNCTION=true flips to SHA512; clear build cache if changed.

**LockFile Format Versions**: v3 (framework-pivoted, legacy) vs v4 (alias-pivoted, modern). One-way conversion; v4 required for current SDK. Set in LockFileFormat.cs constants.

**Non-Serialized Settings**: ProjectRestoreSettings and RestoreAuditProperties excluded from equality/write; set before restore, do not round-trip.

**Packages.lock.json Independence**: Separate hierarchy from LockFile; independent versioning and comparers (LockFileDependencyIdVersionComparer, etc.).

## Build & Test

Build all platforms:
```powershell
dotnet build src\NuGet.Core\NuGet.ProjectModel\NuGet.ProjectModel.csproj -c Release
```

Run targeted serialization tests:
```powershell
dotnet test test\NuGet.Core.Tests\NuGet.ProjectModel.Test\NuGet.ProjectModel.Test.csproj -c Release --filter "LockFileFormat|DependencyGraphSpec" --no-build
```

Verify API surface (PublicAPI.Shipped.txt must be current):
```powershell
dotnet build src\NuGet.Core\NuGet.ProjectModel\NuGet.ProjectModel.csproj -c Release /p:EnforceCodeStyleInBuild=true
```

Test hash regression (FNV64):
```powershell
dotnet test test\NuGet.Core.Tests\NuGet.ProjectModel.Test\NuGet.ProjectModel.Test.csproj -c Release --filter "FnvHash64" --no-build
```
