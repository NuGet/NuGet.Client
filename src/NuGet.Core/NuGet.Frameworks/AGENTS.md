# AGENTS.md: NuGet.Frameworks

## Scope
Framework abstraction and compatibility layer: parsing target frameworks (net, netstandard, netcoreapp, net5.0+, portable, xamarin, uap, tizen), computing nearest-compatible selection via FrameworkReducer, building compatibility graphs.

## Architecture
- **Core Types**: NuGetFramework (immutable TFM model), NuGetFrameworkFactory (Parse/ParseFolder), FrameworkReducer (GetNearest)
- **Compatibility**: DefaultCompatibilityProvider + DefaultFrameworkMappings (synonyms, equivalents, one-way rules), CompatibilityTable, FrameworkExpander
- **Special Frameworks**: FallbackFramework, AssetTargetFallbackFramework, DualCompatibilityFramework (platform-aware net5.0+)
- **Comparers** (comparers/): FrameworkPrecedenceSorter, NuGetFrameworkFullComparer, FrameworkRangeComparer
- **Interfaces** (def/): IFrameworkNameProvider, IFrameworkCompatibilityProvider, IFrameworkMappings

## High-Risk Invariants
1. **Parser Path Duality**: Folder names ("net472", "uap10.0") vs FrameworkName format (",Version=..."). NuGetFrameworkFactory.ParseFolder/ParseFrameworkName must preserve round-trip. Legacy Xamarin/Silverlight identifiers require exact keyword mapping (FrameworkConstants.FrameworkIdentifiers).
2. **Compatibility Graph**: Hardcoded DefaultFrameworkMappings rules. Framework aliases (NETFramework→.NET) and one-way compatibility (net5.0→netstandard2.1 but NOT 2.2) are non-obvious; changes break asset selection.
3. **FrameworkReducer.GetNearest Contract**: Uses IFrameworkCompatibilityProvider to find best match. Handles FallbackFramework/AssetTargetFallbackFramework special cases for platform-specific (net6.0-ios1.0) fallback chains.
4. **Platform Versions (net5.0+)**: net5.0+ NOT compatible with net4.x; requires explicit AssetTargetFallback. Platform-specific TFMs have distinct compatibility (net6.0-ios1.0 ≠ xamarin.ios).

## Test Validation
`powershell
dotnet build src\NuGet.Core\NuGet.Frameworks\NuGet.Frameworks.csproj -c Debug
dotnet test test\NuGet.Core.Tests\NuGet.Frameworks.Test\NuGet.Frameworks.Test.csproj -c Debug --filter "FullyQualifiedName~FrameworkReducerTests|FullyQualifiedName~CompatibilityTests|FullyQualifiedName~NuGetFrameworkParseTests"
`

## Uncertainties
- **Portable Profiles**: DefaultPortableFrameworkMappings hardcodes profile→frameworks; legacy data structure not documented.
- **Xamarin Compatibility**: MonoAndroid, Xamarin.iOS scattered across mappings; exact precedence requires full audit.
- **Platform Version Edge Cases**: net6.0-ios15.0 parsing may have undocumented edge cases in NuGetFrameworkFactory.
