using NuGet.Client;
using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// MEF export interface for SourceRepositoryProvider
    /// </summary>
    public interface ISourceRepositoryProvider
    {
        /// <summary>
        /// Retrieve repositories
        /// </summary>
        /// <returns></returns>
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
