# NuGet.DependencyResolver.Core

**Scope:** PackageReference dependency resolver graph engine. This module owns all graph-based version resolution, cycle detection, and conflict analysis. Public APIs: RemoteDependencyWalker, GraphOperations.Analyze(), RemoteWalkContext, Disposition enum.

## Architecture

**Graph Model:** GraphNode<TItem> with tri-state tracking:
- InnerNodes: Dependencies of this package
- ParentNodes: Parents that requested this package (used for rejected node tracking)
- OuterNode: Nesting context (e.g., multiple versions of same package)

**Disposition States** (immutable progression):
1. Acceptable (initial)
2. Accepted (selected by nearest-wins)
3. Rejected (conflict)
4. PotentiallyDowngraded (superseded)
5. Cycle (circular dependency)

**Resolution Pipeline** (GraphOperations.Analyze):
1. Walk tree depth-first, apply nearest-wins heuristic
2. Mark conflicting versions as Rejected
3. Detect cycles
4. Filter downgrades: only emit if target is Accepted AND all parent chain nodes are Accepted

## High-Risk Invariants

- **Downgrade Relevance:** Must verify DowngradedTo.Disposition == Accepted before emitting downgrade. Orphaned rejections leak if parent disposition is not checked.
- **Nearest Wins:** Version selection at first occurrence in graph tree; later occurrences reuse or reject. Changes to traversal order break resolution.
- **Central Package Versions:** RemoteDependencyWalker collects transitive CPV dependencies in queue; must exclude direct dependencies before adding transitive nodes.

## Test Strategy

Build and run all tests:
```cmd
dotnet build src\NuGet.Core\NuGet.DependencyResolver.Core\NuGet.DependencyResolver.Core.csproj -c Release
dotnet test test\NuGet.Core.Tests\NuGet.DependencyResolver.Core.Tests\NuGet.DependencyResolver.Core.Tests.csproj -v normal
```

Test specific invariants:
```cmd
dotnet test test\NuGet.Core.Tests\NuGet.DependencyResolver.Core.Tests\NuGet.DependencyResolver.Core.Tests.csproj --filter "FullyQualifiedName~GraphOperationsTests" -v normal
dotnet test test\NuGet.Core.Tests\NuGet.DependencyResolver.Core.Tests\NuGet.DependencyResolver.Core.Tests.csproj --filter "FullyQualifiedName~RemoteDependencyWalkerTests" -v normal
```

## Key Dependencies

- NuGet.LibraryModel: LibraryIdentity, LibraryRange, LibraryDependency
- NuGet.Protocol: SourceRepository, SourceCacheContext
- NuGet.Configuration: PackageSource, PackageSourceMapping
- Shared: eng\shared\HashCodeCombiner.cs, TaskResultCache.cs
