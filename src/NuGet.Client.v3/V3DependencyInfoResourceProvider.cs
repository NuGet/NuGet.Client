using NuGet.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Retrieves all dependency info for the package resolver.
    /// </summary>
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(DepedencyInfoResource))]
    public class V3DependencyInfoResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3DependencyInfoResourceProvider()
            : this(new DataClient())
        {

        }

        public V3DependencyInfoResourceProvider(DataClient client)
        {
            _client = client;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            DepedencyInfoResource dependencyInfoResource = null;

            if (source.GetResource<V3ServiceIndexResource>() != null)
            {
                // construct a new resource
                dependencyInfoResource = new V3DependencyInfoResource(_client);
            }

            resource = dependencyInfoResource;
            return resource != null;
        }
    }
}
