# NuGet.Repo.Analyzers

This project contains Roslyn analyzers for enforcing code quality standards in the NuGet.Client repository.

## Analyzers

### NCA0001: Dictionary with string key should specify a StringComparer

**Category**: Usage
**Severity**: Warning

#### Description

Dictionaries with string keys should explicitly specify a `StringComparer` to ensure consistent behavior across different cultures and platforms. Without an explicit comparer, dictionaries use the default comparer which can lead to unexpected behavior in different environments.

#### Examples

**Bad** ❌
```csharp
var dict = new Dictionary<string, int>();
var dict2 = new Dictionary<string, string>(capacity: 10);
var concurrent = new ConcurrentDictionary<string, object>();
```

**Good** ✅
```csharp
var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var dict2 = new Dictionary<string, string>(capacity: 10, StringComparer.Ordinal);
var concurrent = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
```

#### Rationale

String comparisons can vary based on culture and platform. By explicitly specifying a `StringComparer`, you ensure:
- Predictable behavior across different cultures
- Consistent performance characteristics
- Clear intent about case sensitivity and comparison semantics

#### Recommended Comparers

- `StringComparer.Ordinal`: Binary comparison, case-sensitive, fastest
- `StringComparer.OrdinalIgnoreCase`: Binary comparison, case-insensitive
- `StringComparer.CurrentCulture`: Culture-aware comparison
- `StringComparer.InvariantCulture`: Invariant culture comparison

For most scenarios in NuGet.Client, `StringComparer.Ordinal` or `StringComparer.OrdinalIgnoreCase` are recommended.
