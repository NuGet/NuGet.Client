using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
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
    public sealed class SourceRepository
    {
        private readonly Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] _providers;
        private readonly PackageSource _source;

        public SourceRepository(PackageSource source, IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers)
        {
            _source = source;
            _providers = providers.ToArray();
        }

        /// <summary>
        /// Package source
        /// </summary>
        public PackageSource PackageSource
        {
            get
            {
                return _source;
            }
        }

        /// <summary>
        /// Returns a resource from the SourceRepository if it exists.
        /// </summary>
        /// <typeparam name="T">Expected resource type</typeparam>
        /// <returns>Null if the resource does not exist</returns>
        public T GetResource<T>()
        {
            Type resourceType = typeof(T);
            INuGetResource resource = null;

            // TODO: add ordering support here
            foreach (Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata> provider in _providers)
            {
                // return the first provider we find whose output is the requested type, or whose output derives from the type.
                if (resourceType == provider.Metadata.ResourceType || resourceType.IsSubclassOf(provider.Metadata.ResourceType))
                {
                    if (provider.Value.TryCreate(this, out resource))
                    {
                        // found
                        break;
                    }
                }
            }

            return resource == null ? default(T) : (T)resource;
        }

        /// <summary>
        /// Returns a resource from the SourceRepository if it exists.
        /// </summary>
        /// <typeparam name="T">Expected resource type</typeparam>
        /// <returns>Null if the resource does not exist</returns>
        public async Task<T> GetResourceAsync<T>() 
        {
            return await Task.Run(() => GetResource<T>());
        }
    }
}
