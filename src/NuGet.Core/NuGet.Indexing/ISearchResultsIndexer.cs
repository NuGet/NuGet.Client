using NuGet.Protocol.Core.Types;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    /// <summary>
    /// A contract of package metadata ranking provider
    /// </summary>
    public interface ISearchResultsIndexer
    {
        /// <summary>
        /// Associates relevance rank with every element in search results collection
        /// </summary>
        /// <param name="queryString">Relevance ranking criteria</param>
        /// <param name="entries">Search results</param>
        /// <returns>Dictionary of package to rank associations</returns>
        IDictionary<string, int> Rank(string queryString, IEnumerable<IPackageSearchMetadata> entries);
    }
}
