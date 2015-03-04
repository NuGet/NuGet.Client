using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// V3 Simple search resource aimed at command line searches
    /// </summary>
    public class SimpleSearchResourceV3Provider : ResourceProvider
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public SimpleSearchResourceV3Provider()
            : base(typeof(SimpleSearchResource), "SimpleSearchResourceV3Provider", "SimpleSearchResourceV2Provider")
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            SimpleSearchResourceV3 curResource = null;

            var rawSearch = await source.GetResourceAsync<RawSearchResourceV3>(token);

            if (rawSearch != null && rawSearch is RawSearchResourceV3)
            {
                curResource = new SimpleSearchResourceV3(rawSearch);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
