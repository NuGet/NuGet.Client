using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Represents a Server endpoint. Exposes methods to get a specific resource like Search resoure, Metrics service and so on for the given server endpoint.
    /// This will be the replacement for existing SourceRepository class.
    /// </summary>  
    // *TODOs: Define ResourceNotFound exception instead of general exception ? 
    public  class SourceRepository2
    {
        [ImportMany]
        private IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> _providers { get; set; }
        private readonly PackageSource _source;       

        //*TODOs: Providers should be automatically imported when run inside vs context. Right now passing triggering it as part of testapp and passing it as param.
        public SourceRepository2(PackageSource source, IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers) 
            
        {
            _source = source;
            _providers = providers;
        }

        public object GetResource(Type resourceType)
        {            
            foreach(Lazy<ResourceProvider,IResourceProviderMetadata>  provider in _providers)
            {
                //Each provider will expose the "ResourceType" that it can create. Filter the provider based on the current "resourceType" that is requested and invoke TryCreateResource on it.
                if (provider.Metadata.ResourceType == resourceType)
                {
                    Resource resource = null;
                    if (provider.Value.TryCreateResource(_source, out resource))
                    {
                        return resource;
                    }
                }
            }
            return null;
        }
       
        public T GetResource<T>() { return (T)GetResource(typeof(T)); }
    }
}
