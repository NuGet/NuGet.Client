using NuGet.Configuration;
using System.Collections.Generic;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// SourceRepositoryProvider composes resource providers into source repositories.
    /// </summary>
    public interface ISourceRepositoryProvider
    {
        /// <summary>
        /// Retrieve repositories
        /// </summary>
        IEnumerable<SourceRepository> GetRepositories();

        /// <summary>
        /// Create a repository for one time use.
        /// </summary>
        SourceRepository CreateRepository(PackageSource source);

        /// <summary>
        /// Gets the package source provider
        /// </summary>
        IPackageSourceProvider PackageSourceProvider { get; }
    }
}
