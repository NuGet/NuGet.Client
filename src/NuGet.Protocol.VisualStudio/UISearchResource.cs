using NuGet.Protocol.Core.Types;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// Retrieves search metadata in the from used by the VS UI
    /// </summary>
    public abstract class UISearchResource : INuGetResource
    {
        /// <summary>
        /// Retrieves search results
        /// </summary>
         public abstract Task<IEnumerable<UISearchMetadata>> Search(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            CancellationToken cancellationToken);
    }
}
