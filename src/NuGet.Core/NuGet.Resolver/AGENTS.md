# NuGet.Resolver — AGENTS

## Purpose
Constraint Satisfaction Problem (CSP) solver for packages.config dependency resolution. Implements Forward Checking with Conflict-directed Back Jumping (FC-CBJ) to resolve transitive package constraints and validate version conflicts.

## Core Invariants

**CombinationSolver<T> Algorithm**
- FC-CBJ CSP solver with backtracking limit: hard cap at 10,000 iterations returns `null` (no solution) not exception
- Maintains `_currentDomains` initialized from `_initialDomains` per backtrack; forward-checking prunes future domains
- Conflict set tracks past indices for intelligent backjump (CombinationSolver.cs)

**ResolverPackage.Absent Marker**
- Packages marked `Absent=true` represent optional non-targets; version and dependencies **must** be null
- Enforced: `Debug.Assert(!Absent || (version == null && dependencies == null))`
- Allows pruning optional packages after solution found if not needed (PackageResolver.cs, 153)

**DependencyBehavior Resolution Priority**
- Absent packages → Preferred/installed versions → Listed packages → Version ordering (lowest/highest by enum)
- Target packages (explicitly requested) always resolve to Highest version regardless of behavior setting (ResolverComparer.cs)

**Circular Dependency Rejection**
- After topological sort, `FindFirstCircularDependency()` detects cycles and throws `NuGetResolverConstraintException`
- Only non-absent packages checked; enforced post-solution (PackageResolver.cs)

**Input Sorting: TreeFlatten**
- Reorders dependency groups by depth via iterative parent-count tracking before solver entry (ResolverInputSort.cs)
- Enables early constraint discovery

**Preprocessing: RemoveImpossiblePackages**
- Iterative pruning: packages outside worst-case `VersionRange.Combine()` removed until fixed-point (PackageResolver.cs)

## Non-Thread-Safe
PackageResolver instance reuse across threads unsafe. CancellationToken respected at entry and iterations.

## Build & Test Commands

```powershell
# Build library only
dotnet build src\NuGet.Core\NuGet.Resolver\NuGet.Resolver.csproj

# Run all resolver tests
dotnet test test\NuGet.Core.Tests\NuGet.Resolver.Test\NuGet.Resolver.Test.csproj

# Run specific test by pattern (xUnit filter syntax)
dotnet test test\NuGet.Core.Tests\NuGet.Resolver.Test\NuGet.Resolver.Test.csproj --filter "FullyQualifiedName~ResolverTests.ResolveChooseBest"

# Validate PublicAPI contract after changes
dotnet build src\NuGet.Core\NuGet.Resolver\NuGet.Resolver.csproj /p:Configuration=Release
# → Check PublicAPI.Unshipped.txt for breaking changes
```

## Key Test Files
- ResolverTests.cs (core resolution scenarios, circular refs, version conflicts)
- ResolverUtilityTests.cs (diagnostic message generation, dependency satisfaction)
- ResolverSortTests.cs, ResolverInputSortTests.cs (input ordering validation)
