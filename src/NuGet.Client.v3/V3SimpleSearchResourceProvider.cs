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
    /// V3 Simple search resource aimed at command line searches
    /// </summary>
    
    [NuGetResourceProviderMetadata(typeof(SimpleSearchResource), "V3SimpleSearchResourceProvider", "V2SimpleSearchResourceProvider")]
    public class V3SimpleSearchResourceProvider : INuGetResourceProvider
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public V3SimpleSearchResourceProvider()
        {

        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3SimpleSearchResource curResource = null;

            var rawSearch = await source.GetResourceAsync<V3RawSearchResource>(token);

            if (rawSearch != null && rawSearch is V3RawSearchResource)
            {
                curResource = new V3SimpleSearchResource(rawSearch);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
