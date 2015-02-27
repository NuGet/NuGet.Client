using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    /// <summary>
    /// V3 Simple search resource aimed at command line searches
    /// </summary>
    public class V3SimpleSearchResourceProvider : ResourceProvider
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public V3SimpleSearchResourceProvider()
            : base(typeof(SimpleSearchResource), "V3SimpleSearchResourceProvider", "V2SimpleSearchResourceProvider")
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
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
