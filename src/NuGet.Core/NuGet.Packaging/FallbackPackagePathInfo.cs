using System;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public class FallbackPackagePathInfo
    {
        /// <summary>
        /// Path resolver for the root package folder containing this package.
        /// </summary>
        public VersionFolderPathResolver PathResolver { get; }

        /// <summary>
        /// Package id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Package version.
        /// </summary>
        public NuGetVersion Version { get; }

        public FallbackPackagePathInfo(string id, NuGetVersion version, VersionFolderPathResolver resolver)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            Id = id;
            Version = version;
            PathResolver = resolver;
        }
    }
}
