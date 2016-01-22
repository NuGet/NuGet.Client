using NuGet.Protocol.Core.Types;
using System.Collections.Generic;

namespace NuGet.Protocol.VisualStudio
{
    public interface ISearchResultsAggregator : INuGetResource
    {
        IEnumerable<PackageSearchMetadata> Aggregate(string queryString, params IEnumerable<PackageSearchMetadata>[] results);
    }
}
