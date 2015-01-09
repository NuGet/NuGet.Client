using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Simple search results for V3
    /// </summary>
    public class V3SimpleSearchResource : SimpleSearchResource
    {
        private readonly V3RawSearchResource _rawSearch;

        public V3SimpleSearchResource(V3RawSearchResource rawSearch)
        {
            _rawSearch = rawSearch;
        }

        /// <summary>
        /// Basic search
        /// </summary>
        public override async Task<IEnumerable<SimpleSearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            List<SimpleSearchMetadata> results = new List<SimpleSearchMetadata>();

            foreach (var result in await _rawSearch.Search(searchTerm, filters, skip, take, cancellationToken))
            {
                NuGetVersion version = NuGetVersion.Parse(result["version"].ToString());
                PackageIdentity identity = new PackageIdentity(result["id"].ToString(), version);

                SimpleSearchMetadata data = new SimpleSearchMetadata(identity, result["description"].ToString());

                results.Add(data);
            }

            return results;
        }
    }
}
