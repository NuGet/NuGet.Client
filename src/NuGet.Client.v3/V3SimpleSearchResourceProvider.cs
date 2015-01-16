using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// V3 Simple search resource aimed at command line searches
    /// </summary>
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(SimpleSearchResource), "V3SimpleSearchResourceProvider", "V2SimpleSearchResourceProvider")]
    public class V3SimpleSearchResourceProvider : INuGetResourceProvider
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public V3SimpleSearchResourceProvider()
        {

        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3SimpleSearchResource curResource = null;

            var rawSearch = source.GetResource<V3RawSearchResource>();

            if (rawSearch != null && rawSearch is V3RawSearchResource)
            {
                curResource = new V3SimpleSearchResource(rawSearch);
            }

            resource = curResource;
            return curResource != null;
        }
    }
}
