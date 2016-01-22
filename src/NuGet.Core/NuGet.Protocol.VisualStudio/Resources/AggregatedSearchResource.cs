using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace NuGet.Protocol.VisualStudio
{
    public class AggregatedSearchResource : ISearchResource
    {
        private readonly ISearchResource[] _searchResources;

        public AggregatedSearchResource(IEnumerable<ISearchResource> searchResources)
        {
            _searchResources = searchResources.ToArray();
        }

        public Task<IEnumerable<PackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
