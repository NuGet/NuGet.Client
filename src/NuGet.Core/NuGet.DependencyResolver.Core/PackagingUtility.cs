using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public static class PackagingUtility
    {
        /// <summary>
        /// Convert a nuspec dependency to a library dependency.
        /// </summary>
        public static LibraryDependency GetLibraryDependencyFromNuspec(Packaging.Core.PackageDependency dependency)
        {
            // Start with the default
            var includeType = ~LibraryIncludeFlags.ContentFiles;

            // Add includes
            if (dependency.Include.Count > 0)
            {
                includeType = LibraryIncludeFlagUtils.GetFlags(dependency.Include);
            }

            // Remove excludes
            if (dependency.Exclude.Count > 0)
            {
                var excludeType = LibraryIncludeFlagUtils.GetFlags(dependency.Exclude);

                includeType = includeType & ~excludeType;
            }

            // Create the library
            // Nuspec references cannot contain suppress parent flags
            var libraryDependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = dependency.Id,
                    VersionRange = dependency.VersionRange
                },
                IncludeType = includeType,
                SuppressParent = LibraryIncludeFlags.None
            };

            return libraryDependency;
        }
    }
}
