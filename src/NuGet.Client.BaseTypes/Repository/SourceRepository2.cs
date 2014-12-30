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

        public PackageSource Source
        {
            get;
            private set;
        }

        //*TODOs: Providers should be automatically imported when run inside vs context. Right now passing triggering it as part of testapp and passing it as param.
        public SourceRepository2(PackageSource source, IEnumerable<Lazy<ResourceProvider, IResourceProviderMetadata>> providers)             
        {           
            Source = source;
            _providers = providers;
        }

        public async Task<object> GetResource(Type resourceType)
        {            
            foreach(Lazy<ResourceProvider,IResourceProviderMetadata>  provider in _providers)
            {
                //Each provider will expose the "ResourceType" that it can create. Filter the provider based on the current "resourceType" that is requested and invoke TryCreateResource on it.
                if (provider.Metadata.ResourceType == resourceType)
                {
                    Resource resource = await provider.Value.Create(Source);
                    if (resource != null)
                        return resource;
                }
            }
            return null;
        }
       
        public async Task<T> GetResource<T>() 
        {
           object x = await GetResource(typeof(T));
           return (T)x;            
        }
    }
}
