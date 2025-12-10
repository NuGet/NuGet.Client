# Case sensitive string comparisons

The `string` type could represent package names, filesystem paths, json properties, xml elements, or many other things.
Some of these need to compared in a case sensitive manner, while others need to be compared in a case insensitive manner.
We have introduced analyzers to enforce every string comparison to be explicit about whether it is case sensitive or not.

Filesystem paths are a special case, because the default filesystem on Windows is case insensitive, while the default file system on Linux and MacOS is case sensitive.
Therefore, we have a `PathUtil.GetStringComparisonForCurrentPlatform()` method that returns the appropriate `StringComparison` value for the current platform.

NuGet treats package identifiers (names), package versions, and package source names (but not their URLs) as case insensitive.
So, in contexts when these are being looked up or deduplicated, use `StringComparison.OrdinalIgnoreCase`.

XML and JSON are case sensitive, so if writing code that process these manually, use a case sensitive comparison.
But it's better to use automatic serialization and deserialization, so prefer that where possible.

There are contexts, like modifying a NuGet.config file, updating a project file, or updating a GUI view, where we should use a case sensitive comparison for "up to date" checks.
If the customer performs an action that intends to change the case for a package id, NuGet should perform that action, even if from restore's poitn of view it's the same.
