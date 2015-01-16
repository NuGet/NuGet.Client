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
    [NuGetResourceProviderMetadata(typeof(DepedencyInfoResource), "V3DependencyInfoResourceProvider", "V2DependencyInfoResourceProvider")]
    public class V3DependencyInfoResourceProvider : INuGetResourceProvider
    {
        public V3DependencyInfoResourceProvider()
        {

        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            DepedencyInfoResource dependencyInfoResource = null;

            if (source.GetResource<V3ServiceIndexResource>() != null)
            {
                DataClient client = new DataClient(source.GetResource<HttpHandlerResource>().MessageHandler);

                var regResource = source.GetResource<V3RegistrationResource>();

                // construct a new resource
                dependencyInfoResource = new V3DependencyInfoResource(client, regResource);
            }

            resource = dependencyInfoResource;
            return resource != null;
        }
    }
}
