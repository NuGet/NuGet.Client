# NuGet.LibraryModel

## Architecture

**Location**: src\NuGet.Core\NuGet.LibraryModel\
**Purpose**: Core dependency model types and interfaces for NuGet resolution
**Scope**: 23 source files; 8 shared build files (EqualityUtility, HashCodeCombiner, IsExternalInit, NullableAttributes, RequiredModifierAttributes, SimplePool, StringBuilderPool, NoAllocEnumerateExtensions)

## High-Risk Invariants

**LibraryIdentity** (public, IEquatable, IComparable)
- Equality contract: Name (OrdinalIgnoreCase), Version, Type all must match
- HashCode: case-insensitive name, Version, Type via HashCodeCombiner
- Used as primary key in Library.IdentityComparer

**LibraryDependency** (public, init-only properties)
- Equality contract: 10 fields must match (LibraryRange, IncludeType, SuppressParent, NoWarn, AutoReferenced, GeneratePathProperty, VersionCentrallyManaged, Aliases, VersionOverride, ReferenceType)
- NoWarn: ImmutableArray<NuGetLogCode> must match by sequence, not reference
- Mutable field _noWarn normalized on init (default empty array, never null)

**LibraryRange** (public, init-only required Name)
- Equality: Name, VersionRange (null-safe), TypeConstraint
- ToString emits type constraint prefix when Package|Project|ExternalProject|Reference

**Library**
- IdentityComparer uses Identity.Equals only; Path and Resolved are not compared
- Items dictionary untracked in equality

## Dependencies

- NuGet.Common (NuGetLogCode)
- NuGet.Versioning (VersionRange, NuGetVersion)
- System.Collections.Immutable (conditional, net472+)

## Test Commands

**Build**:
`
dotnet build src\NuGet.Core\NuGet.LibraryModel\NuGet.LibraryModel.csproj -c Release
`

**Test**:
`
dotnet test test\NuGet.Core.Tests\NuGet.LibraryModel.Tests\NuGet.LibraryModel.Tests.csproj -c Release --filter "Category!=Integration"
`

**API Stability** (PublicAPI.Shipped.txt enforced):
`
dotnet build src\NuGet.Core\NuGet.LibraryModel\NuGet.LibraryModel.csproj /p:EnforceCodeStyleInBuild=true -c Release
`

## InternalsVisibleTo

- NuGet.Commands.Test
- NuGet.ProjectModel.Test
