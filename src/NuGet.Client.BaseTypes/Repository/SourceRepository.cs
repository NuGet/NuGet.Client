using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Client
{
    /// <summary>
    /// Represents a Server endpoint. Exposes the list of resources/services provided by the endpoint like : Search service, Metrics service and so on.
    /// </summary>
    // TODO: it needs to implement IDisposable.
    // TODO: Define RequiredResourceNotFound exception instead of general exception.    
    public abstract class SourceRepository
    {
        public abstract PackageSource Source { get; }
        private readonly Dictionary<Type, Func<object>> _resourceFactories = new Dictionary<Type, Func<object>>();
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public abstract Task<IEnumerable<JObject>> Search(
            string searchTerm,
            // REVIEW: Do we use parameters instead of this object? What about adding filter criteria?
            SearchFilter filters,
            int skip,
            int take,
            CancellationToken cancellationToken);

        public abstract Task<JObject> GetPackageMetadata(string id, NuGetVersion version);
        public abstract Task<IEnumerable<JObject>> GetPackageMetadataById(string packageId);
        public abstract void RecordMetric(PackageActionType actionType, PackageIdentity packageIdentity, PackageIdentity dependentPackage, bool isUpdate, IInstallationTarget target);

        /// <summary>
        /// Retrieves an instance of the requested resource, throwing a <see cref="RequiredResourceNotSupportedException"/>
        /// if the resource is not supported by this host.
        /// </summary>
        /// <typeparam name="T">The type defining the resource to retrieve</typeparam>
        /// <returns>An instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="RequiredResourceNotSupportedException">The target does not support this resource.</exception>
        public virtual T GetRequiredResource<T>()
        {
            var resource = TryGetResource<T>();
            if (resource == null)
            {
                throw new Exception(String.Format("Resource {0} in the current Source {1}", typeof(T).FullName, Source.Name));
            }
            return resource;
        }

        /// <summary>
        /// Retrieves an instance of the requested resource, throwing a <see cref="RequiredResourceNotSupportedException"/>
        /// if the resource is not supported by this host.
        /// </summary>
        /// <param name="resourceType">The type defining the resource to retrieve</param>
        /// <returns>An instance of <paramref name="resourceType"/>.</returns>
        /// <exception cref="RequiredResourceNotSupportedException">The host does not support this resource.</exception>
        public virtual object GetRequiredResource(Type resourceType)
        {
            var resource = TryGetResource(resourceType);
            Debug.Assert(resource != null, "Required resource '" + resourceType.FullName + "' not found for this source " + Source.Name);
            if (resource == null)
            {
                throw new Exception(String.Format("Resource {0} in the current Source {1}", resourceType.FullName, Source.Name));
            }
            return resource;
        }

        /// <summary>
        /// Retrieves an instance of the requested resource, if one exists in this host.
        /// </summary>
        /// <typeparam name="T">The type defining the resource to retrieve</typeparam>
        /// <returns>An instance of <typeparamref name="T"/>, or null if no such resource exists.</returns>
        public virtual T TryGetResource<T>() { return (T)TryGetResource(typeof(T)); }

        /// <summary>
        /// Retrieves an instance of the requested resource, if one exists in this host.
        /// </summary>
        /// <param name="resourceType">The type defining the resource to retrieve</param>
        /// <returns>An instance of <paramref name="resourceType"/>, or null if no such resource exists.</returns>
        public virtual object TryGetResource(Type resourceType)
        {
            Func<object> factory;
            if (!_resourceFactories.TryGetValue(resourceType, out factory))
            {
                return null;
            }
            return factory();
        }

        protected void AddResource<T>(Func<T> factory) where T : class
        {
            _resourceFactories.Add(typeof(T), factory);
        }
    }
}
