using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio
{
    public abstract class UIMetadataResource : INuGetResource
    {
        /// <summary>
        /// Returns all versions of a package
        /// </summary>
        public abstract Task<IEnumerable<UIPackageMetadata>> GetMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token);
    }
}