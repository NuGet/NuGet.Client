using System;
using NuGet.Repositories;

namespace NuGet.Commands
{
    /// <summary>
    /// This class contains resources which may be shared between restores.
    /// ALL resources here are optional and may be null. They are internal
    /// caches used by the RestoreCommand. Adding them here will allow multiple restore
    /// commands to benefit from the same cache.
    /// If they do not exist the RestoreCommand will create them on a per project basis.
    /// </summary>
    public class RestoreCommandSharedCache
    {
        public RestoreCommandSharedCache()
        {
        }

        public RestoreCommandSharedCache(NuGetv3LocalRepository localCache)
        {
            if (localCache == null)
            {
                throw new ArgumentNullException(nameof(localCache));
            }

            LocalCache = localCache;
        }

        /// <summary>
        /// A <see cref="NuGetv3LocalRepository"/> repository may be passed in as part of the request.
        /// This allows multiple restores to share the same cache for the global packages folder
        /// and reduce disk hits.
        /// </summary>
        public NuGetv3LocalRepository LocalCache { get; set; }
    }
}
