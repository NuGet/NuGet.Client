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
            var includeType = LibraryIncludeType.Default;

            // Add includes
            if (dependency.Include.Count > 0)
            {
                includeType = LibraryIncludeType.Parse(dependency.Include);
            }

            // Remove excludes
            if (dependency.Exclude.Count > 0)
            {
                includeType = includeType.Except(
                    LibraryIncludeType.Parse(dependency.Exclude));
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
                SuppressParent = LibraryIncludeType.None
            };

            return libraryDependency;
        }
    }
}
