NuGet's core types and interfaces for PackageReference-based restore, such as lock files, assets file and internal restore models.

Package-spec serialization reuses the canonical ordering of central package versions and pruning references on each `TargetFrameworkInformation`.
The cached ordering is validated against the backing dictionary before reuse, since an `IReadOnlyDictionary` can wrap mutable data.
Changes to entries, key casing, or source enumeration order rebuild the ordering without changing the serialized format or restore hash.
Concurrent readers can share the cached ordering; mutating a dictionary concurrently with serialization remains unsupported.

Hashing also caches the JSON text for these sections on the validated ordering snapshot on their third hash request.
Sections used only once or twice continue to be written directly, without retaining JSON text.
Replaying text through the existing hash writer avoids repeated escaping while retaining the original encoding and hash-update boundaries.
Temporary serialization buffers are reused within a hash operation and released when its writer is disposed.
Normal JSON serialization still uses the caller's writer and formatting settings.
Within a hash operation, equivalent sections share one serialization and cached representation.
Content matching uses the complete ordered, case-sensitive names and the exact version text that is written to JSON, not just a hash code.
The operation lazily seeds existing immutable sections from its project models so sharing does not depend on project traversal order.
Operation-local indexes and their project references are released when the hash writer is disposed; there is no process-wide content cache.
Sections without immutable original version text use normal serialization, including custom version-range formatters that can change their generated text.
Cached text adds retained memory and a one-time construction cost.
Higher reuse amortizes the construction cost; inputs with little reuse stay on the direct serialization path.
