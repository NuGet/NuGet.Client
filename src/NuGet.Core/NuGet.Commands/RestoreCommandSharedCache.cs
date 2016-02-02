using System.Collections.Generic;
using NuGet.DependencyResolver;
using NuGet.Repositories;

namespace NuGet.Commands
{
    /// <summary>
    /// This class contains resources which may be shared between restores.
    /// </summary>
    public class RestoreCommandSharedCache
    {
        /// <summary>
        /// A <see cref="NuGetv3LocalRepository"/> repository may be passed in as part of the request.
        /// This allows multiple restores to share the same cache for the global packages folder
        /// and reduce disk hits.
        /// </summary>
        /// <remarks>This is optional and may be null.</remarks>
        public NuGetv3LocalRepository LocalCache { get; set; }

        /// <summary>
        /// Remote dependency providers that can be shared between restores.
        /// This allows a single <see cref="HttpSource"/> to be used per source.
        /// </summary>
        /// <remarks>This is optional and may be empty.</remarks>
        public IList<IRemoteDependencyProvider> RemoteProviders { get; set; }
            = new List<IRemoteDependencyProvider>();
    }
}
