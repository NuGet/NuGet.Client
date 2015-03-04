using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Retrieves the latest package metadata from search. This differs from SearchLatestResource in that 
    /// it does not return all versions.
    /// </summary>
    /// <remarks>Equivalent to the legacy V2 search</remarks>
    public abstract class SearchLatestResource : INuGetResource
    {
        /// <summary>
        /// Retrieves search results
        /// </summary>
        public abstract Task<IEnumerable<ServerPackageMetadata>> Search(
           string searchTerm,
           SearchFilter filters,
           int skip,
           int take,
           CancellationToken cancellationToken);
    }
}
