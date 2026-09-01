# NuGet.Indexing

Package search indexing and ranking for Visual Studio client.

## Architecture

**Query Flow**: Query string → NuGetQuery.MakeQuery() → Lucene BooleanQuery
- Field keywords: `id`, `version`, `title`, `description`, `tag/tags`, `author/authors`, `summary`, `owner/owners`
- Quoted phrases supported; unquoted tokens become wildcard

**Indexing**: PackageAnalyzer wraps field-specific analyzers
- `Id`, `TokenizedId`, `ShingledId`: IdentifierKeywordAnalyzer, IdentifierAnalyzer
- `Title`, `Description`, `Summary`, `Authors`: DescriptionAnalyzer
- `Version`: VersionAnalyzer (semantic)
- `Tags`: TagsAnalyzer; `Owner`: OwnerAnalyzer

**Ranking Pipeline**
1. RelevanceSearchResultsIndexer.Rank() creates in-memory Lucene index from IPackageSearchMetadata entries
2. Executes query, scores results, maps by package ID to rank dict
3. ProcessUnrankedEntries() fills gaps: unranked entries inherit prior ranked entry's rank, fallback to -1

**Result Aggregation**: SearchResultsAggregator merges multiple search results
- Requires ISearchResultsIndexer (ranking) + IPackageSearchMetadataSplicer (merge strategy)
- Preserves input relative order per feed; re-ranks merged set

## High-Risk Invariants

**PackageSearchMetadataSplicer.MergeEntries(lhs, rhs)**
- Throws InvalidOperationException if lhs.Identity.Id ≠ rhs.Identity.Id (case-insensitive)
- Picks newer version as base; merges version lists via GetVersionsAsync()
- Validates before merge; no partial state on exception

**RelevanceSearchResultsIndexer.Rank()**
- Lucene RAMDirectory is in-memory only; index lifecycle tied to indexer call
- Ranking dict uses case-sensitive package IDs (retrieved from Lucene Field "Id")
- Default rank for unranked entries: -1

## Build & Test

```powershell
# Build
dotnet build src\NuGet.Clients\NuGet.Indexing\NuGet.Indexing.csproj

# Test all
dotnet test test\NuGet.Clients.Tests\NuGet.Indexing.Test\NuGet.Indexing.Test.csproj -v normal

# Test by name
dotnet test test\NuGet.Clients.Tests\NuGet.Indexing.Test\NuGet.Indexing.Test.csproj --filter MergeEntries_WithDifferentPackageIds_Throws
dotnet test test\NuGet.Clients.Tests\NuGet.Indexing.Test\NuGet.Indexing.Test.csproj --filter AggregateAsync_MergesVersions
dotnet test test\NuGet.Clients.Tests\NuGet.Indexing.Test\NuGet.Indexing.Test.csproj --filter ProcessUnrankedEntries_FillsWithDefaultRank
```
