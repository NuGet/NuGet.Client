# Asset Classification Performance Optimization

- @kartheekp-ms
- https://github.com/NuGet/Client.Engineering/issues/2780

## Summary

This proposal introduces a decision-tree-based asset classifier to replace the current O(n×m) pattern-matching algorithm used during NuGet package restore. The new approach reduces complexity to O(n×d) where d is tree depth (~4-5), significantly reducing CPU time and allocations in hot paths identified during Visual Studio solution load profiling.

## Motivation

Profiling data from PR [#5676](https://github.com/NuGet/NuGet.Client/pull/5676) identified `PatternExpression.TokenSegment.TryMatch` as a critical hot path with **99+ million allocations** during large solution loads. The current pattern-matching implementation:

1. Checks every asset against every pattern in each PatternSet
2. Creates substring allocations for each segment match attempt
3. Re-parses target frameworks repeatedly across pattern matches
4. Scales poorly with package count and asset density

**Expected Outcome:** 3-5x reduction in asset classification time during restore operations, with measurable improvements in VS solution load and `dotnet restore` performance.

## Explanation

### Functional explanation

When NuGet restores a package, it must classify hundreds of files into categories like runtime assemblies, compile references, native libraries, etc. 

**Current behavior:** For each asset type you need, scan ALL files against ALL patterns for that type.

```
To find RuntimeAssemblies in a 500-file package:
  → Check all 500 files against 3 patterns = 1,500 checks
  
To find CompileRefAssemblies:
  → Check all 500 files against 2 patterns = 1,000 checks
  
To find NativeLibraries:
  → Check all 500 files against 2 patterns = 1,000 checks
  
Total: 3,500+ pattern match operations
```

**New behavior:** Classify each file exactly once based on its path structure.

```
Classify all 500 files in single pass:
  "lib/net8.0/MyLib.dll"           → RuntimeAssembly
  "ref/net8.0/MyLib.dll"           → CompileRefAssembly  
  "runtimes/win-x64/native/foo.dll" → NativeLibrary
  ...
  
Total: 500 classification operations
```

The classifier uses the first path segment (`lib/`, `ref/`, `runtimes/`, etc.) to route directly to type-specific logic, avoiding redundant checks.

### Technical explanation

#### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    ContentItemCollection                     │
├─────────────────────────────────────────────────────────────┤
│  PopulateItemGroups(PatternSet, IList<ContentItemGroup>)    │
│         │                                                    │
│         ▼                                                    │
│  ┌──────────────────┐    ┌────────────────────────────────┐ │
│  │ Feature Flag OFF │    │ Feature Flag ON                │ │
│  │ (Pattern Match)  │    │ (Decision Tree)                │ │
│  └────────┬─────────┘    └───────────────┬────────────────┘ │
│           │                              │                   │
│           ▼                              ▼                   │
│  PatternExpression.Match()      AssetClassifier.Classify()  │
│  - Iterate segments             - Route by root folder      │
│  - Parse each token             - Direct method dispatch    │
│  - O(n × m × s)                 - O(n × d)                  │
└─────────────────────────────────────────────────────────────┘
```

#### Key Components

**1. AssetClassifier** (`ContentModel/AssetClassifier.cs`)
- Sealed internal class with decision tree logic
- Routes paths by first segment: `lib/`, `ref/`, `runtimes/`, `build/`, `contentFiles/`, `tools/`, `embed/`
- Uses `Span<char>` for zero-allocation string comparisons
- Shares framework cache with `ManagedCodeConventions`

**2. AssetType Enum**
```csharp
public enum AssetType
{
    None,
    RuntimeAssembly,      // lib/{tfm}/{assembly}
    CompileRefAssembly,   // ref/{tfm}/{assembly}
    CompileLibAssembly,   // lib/{tfm}/{assembly} (compile context)
    ResourceAssembly,     // lib/{tfm}/{locale}/{satellite}
    NativeLibrary,        // runtimes/{rid}/native/{file}
    MSBuildFile,          // build/{tfm}/*.props|.targets
    MSBuildMultiTargetingFile,
    MSBuildTransitiveFile,
    ContentFile,          // contentFiles/{codeLanguage}/{tfm}/{file}
    ToolsAssembly,        // tools/{tfm}/{rid}/{file}
    EmbedAssembly         // embed/{tfm}/{assembly}
}
```

**3. Feature Flag** (`ContentModel/ContentModelFeatureFlags.cs`)
```csharp
Environment variable: NUGET_USE_OPTIMIZED_ASSET_CLASSIFIER=true
```

**4. Integration Point** (`ContentItemCollection.PopulateItemGroups`)
```csharp
// New public overload
public void PopulateItemGroups(
    PatternSet definition, 
    IList<ContentItemGroup> contentItemGroupList, 
    ManagedCodeConventions conventions)
```

#### Algorithm Comparison

| Aspect | Pattern Matching | Decision Tree |
|--------|-----------------|---------------|
| Time Complexity | O(n × m × s) | O(n × d) |
| n = assets | 500 | 500 |
| m = patterns per set | 2-4 | 1 |
| s = segments per pattern | 3-6 | N/A |
| d = tree depth | N/A | 4-5 |
| String allocations | Per segment match | Minimal (Span-based) |
| Framework parsing | Repeated | Cached |

#### Example: Classifying `lib/net8.0/MyLib.dll`

**Before (Pattern Matching):**
```
PatternSet.RuntimeAssemblies contains:
  Pattern 1: "runtimes/{rid}/lib/{tfm}/{any?}"
  Pattern 2: "lib/{tfm}/{any?}"
  Pattern 3: "lib/{assembly}"

Step 1: Try Pattern 1
  - Match "runtimes" vs "lib" → FAIL
  
Step 2: Try Pattern 2  
  - Match "lib" literal → OK
  - Parse "{tfm}" token → allocate substring, call NuGetFramework.Parse() → net8.0
  - Parse "{any?}" token → allocate substring → "MyLib.dll"
  - MATCH → Create ContentItem

Step 3: Try Pattern 3 (still checked)
  - Match "lib" → OK
  - Parse "{assembly}" → ...
```

**After (Decision Tree):**
```csharp
Classify("lib/net8.0/MyLib.dll"):
  
Step 1: path.IndexOf('/') → 3, root = "lib"

Step 2: root.Equals("lib") → true, call ClassifyLib()

Step 3: ClassifyLib():
  - Find next delimiter → index 11
  - tfmPart = "net8.0" (as Span, no allocation)
  - Cache lookup for framework → hit or parse once
  - assemblyPart = "MyLib.dll" (as Span)
  - Return ContentItem with AssetType.RuntimeAssembly
```

## Drawbacks

1. **Code Duplication:** Pattern knowledge now exists in two places - the original pattern strings and the classifier logic. Changes to patterns require updating both.

2. **Maintenance Burden:** Adding new asset folder structures (e.g., `analyzers/`) requires code changes to AssetClassifier, not just adding a pattern string.

3. **Testing Complexity:** Must maintain comparative tests to ensure behavioral equivalence between both paths.

4. **Feature Flag Overhead:** Checking the environment variable adds minimal overhead, though it's cached after first read.

## Rationale and alternatives

### Why this design?

1. **Proven hot path:** Profiling data specifically identified pattern matching as the bottleneck
2. **Stable patterns:** NuGet's folder conventions (`lib/`, `ref/`, etc.) haven't changed significantly in years
3. **Safe rollout:** Feature flag allows gradual enablement and instant rollback
4. **Behavioral equivalence:** 92 comparative tests verify identical output

### Alternatives considered

**Alternative 1: Optimize PatternExpression**
- Add caching within `TokenSegment.TryMatch`
- Pre-compile patterns to avoid runtime parsing
- **Rejected:** Still O(n×m) complexity; doesn't eliminate redundant checks

**Alternative 2: Trie-based pattern matching**
- Build trie from all patterns, match in single pass
- **Rejected:** Higher implementation complexity; patterns have variable tokens that complicate trie structure

**Alternative 3: Regex compilation**
- Convert patterns to compiled regex
- **Rejected:** Regex has its own overhead; doesn't leverage domain knowledge about fixed folder structures

### Impact of not doing this

- VS solution load times remain suboptimal for large solutions
- `dotnet restore` continues to be slower than necessary
- Hot path allocations contribute to GC pressure

## Prior Art

1. **Hackathon exploration** (`dev-nyenework-add-assets-tree` branch): Initial proof-of-concept that validated the approach

2. **Issue #2780 analysis:** Detailed profiling showing `TryMatch` as dominant in restore traces

3. **Similar optimizations in other package managers:**
   - npm uses hardcoded folder structure knowledge for `node_modules` layout
   - Maven has fixed `src/main/java`, `src/test/java` conventions baked into tooling

## Unresolved Questions

1. **Benchmark validation:** Need to run end-to-end performance benchmarks comparing pattern matching vs decision tree on real-world packages (EntityFrameworkCore, Newtonsoft.Json, etc.)

2. **Edge cases:** Are there any exotic package structures in the wild that the classifier might handle differently than pattern matching?

3. **Feature flag default:** Should this be opt-in (current) or opt-out after validation?

4. **Public API surface:** The new `PopulateItemGroups` overload is added to public API. Is this the right integration point?

## Future Possibilities

1. **Remove pattern matching path:** Once validated at scale, the feature flag and pattern matching code path could be removed entirely

2. **Extend to other operations:** Similar decision tree approach could optimize `FindBestItemGroup` selection logic

3. **Build-time code generation:** Generate classifier code from pattern definitions to eliminate duplication

4. **Memory-mapped package reading:** Combined with classifier, could enable streaming classification without loading full package manifest

5. **Parallel classification:** Decision tree is inherently parallelizable - could classify assets across multiple threads
