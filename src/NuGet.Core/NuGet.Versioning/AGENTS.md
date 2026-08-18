# AGENTS.md: NuGet.Versioning

## Architecture

**Hybrid SemVer Model**: `SemanticVersion` enforces strict SemVer (3 parts only); `NuGetVersion` extends to legacy 4-digit .NET versioning while tracking `IsSemVer2` property. `OriginalVersion` string preserved to maintain input form (e.g., "1.0" vs "1.0.0.0").

**Normalized vs. Full Strings**: `ToNormalizedString()` strips build metadata (identity-unique); `ToFullString()` preserves it (non-unique). Both delegate to `VersionFormatter` IFormatProvider.

**Parse Caching (Thread-Safe)**: Both `NuGetVersion` and `VersionRange` maintain static `Dictionary` caches (500 entry limit, guarded by `lock()`). Cache clears on overflow to prevent unbounded memory growth.

## High-Risk Invariants

- **Range Inclusivity**: Min/Max bounds tracked as separate booleans (`IsMinInclusive`, `IsMaxInclusive`); range syntax `[1.0, 2.0]` = inclusive, `(1.0, 2.0)` = exclusive.
- **FloatRange Validation**: If `floatBehavior != None` and `!= AbsoluteLatest`, constructor requires `releasePrefix`. `IncludePrerelease` property derived from enum (maps to `Prerelease*`, `AbsoluteLatest` variants only).
- **Satisfies() Comparison Modes**: Four modes via `VersionComparison` enum (Default, Version, VersionRelease, VersionReleaseMetadata). Mode affects label and metadata equality checks.
- **Nullable Codegen**: Full `#nullable enable`; PublicAPI.Shipped.txt enforces surface. Roslyn analyzer (Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4) validates additions/deletions.

## Build & Test

**Targets**: net472, net8.0 (both library and unit tests); LangVersion 14; TreatWarningsAsErrors=true.

**Validate Changes**:
```
dotnet build src\NuGet.Core\NuGet.Versioning\NuGet.Versioning.csproj
dotnet test test\NuGet.Core.Tests\NuGet.Versioning.Test\NuGet.Versioning.Test.csproj --verbosity normal
dotnet test test\NuGet.Core.Tests\NuGet.Versioning.Test\NuGet.Versioning.Test.csproj --filter "VersionRangeFloatParsingTests" --verbosity normal
```

**Key Test Classes**: NuGetVersionTest, VersionRangeTests, SemVer201SpecTests, VersionRangeFloatParsingTests, VersionComparerTests.

## Shared Code

Linked from `build/Shared/`: HashCodeCombiner, NoAllocEnumerateExtensions, NullableAttributes, StringBuilderPool. Embedded resources: Resources.resx + 13 localization .xlf files.
