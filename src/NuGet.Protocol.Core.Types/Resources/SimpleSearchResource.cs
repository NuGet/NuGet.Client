using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Returns basic search results from the source
    /// </summary>
    public abstract class SimpleSearchResource : INuGetResource
    {
        /// <summary>
        /// Returns search entries 
        /// </summary>
        public abstract Task<IEnumerable<SimpleSearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken);
    }
}
