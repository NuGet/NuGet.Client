; Unshipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### NuGet Client Analysis Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NCA0001 | Usage | Warning | DictionaryStringKeyComparerAnalyzer: Dictionary with string key should specify a StringComparer
NCA0002 | Usage | Warning | StringGetHashCodeAnalyzer: Use StringComparer.GetHashCode instead of string.GetHashCode
NCA0003 | Usage | Warning | HashSetStringComparerAnalyzer: HashSet with string element should specify a StringComparer

### Use explicit equality comparers and comparisons with strings

NuGet treats package idenfiers and version strings as case insensitive, so must use StringComparer.OrdinalIgnoreCase or StringComparison.OrdinalIgnoreCase when comparing strings in these contexts.
Different operating systems have different case sensitivity rules for file systems, so file path comparisons should use PathUtility.GetStringComparisonBasedOnOS().
MSBuild treats property names as case insensitive, and we treat all item idenfiers as case insensitive as well.
NuGet.Config source names are also case insensitive.

To minimize risk of bugs related to incorrect string comparisons, these analyzers flag usages of string equality comparisons and collections that do not specify an explicit StringComparer or StringComparison.
