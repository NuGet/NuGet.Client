using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    /// <summary>
    /// Simple search results for V3
    /// </summary>
    public class V3SimpleSearchResource : SimpleSearchResource
    {
        private readonly V3RawSearchResource _rawSearch;

        public V3SimpleSearchResource(V3RawSearchResource rawSearch)
        {
            if (rawSearch == null)
            {
                throw new ArgumentNullException("rawSearch");
            }

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

                string description = result["description"].ToString();

                List<NuGetVersion> allVersions = new List<NuGetVersion>();

                foreach (var versionObj in ((JArray)result["versions"]))
                {
                    allVersions.Add(NuGetVersion.Parse(versionObj["version"].ToString()));
                }

                SimpleSearchMetadata data = new SimpleSearchMetadata(identity, description, allVersions);

                results.Add(data);
            }

            return results;
        }
    }
}
