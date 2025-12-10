; Unshipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NCA0001 | Usage | Warning | DictionaryStringKeyComparerAnalyzer: Dictionary with string key should specify a StringComparer
NCA0002 | Usage | Warning | StringGetHashCodeAnalyzer: Use StringComparer.GetHashCode instead of string.GetHashCode
NCA0003 | Usage | Warning | HashSetStringComparerAnalyzer: HashSet with string element should specify a StringComparer
