NuGet's core types and interfaces for PackageReference-based restore, such as lock files, assets file and internal restore models.

Package-spec serialization reuses the canonical ordering of central package versions and pruning references on each `TargetFrameworkInformation`.
The cached ordering is validated against the backing dictionary before reuse, since an `IReadOnlyDictionary` can wrap mutable data.
Changes to entries, key casing, or source enumeration order rebuild the ordering without changing the serialized format or restore hash.
Concurrent readers can share the cached ordering; mutating a dictionary concurrently with serialization remains unsupported.
