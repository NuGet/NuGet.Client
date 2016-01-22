using NuGet.Protocol.Core.Types;
using System.Collections.Generic;

namespace NuGet.Protocol.VisualStudio
{
    public interface ISearchResultsIndexer : INuGetResource
    {
        IDictionary<string, int> Rank(string queryString, IEnumerable<PackageSearchMetadata> entries);
    }
}
