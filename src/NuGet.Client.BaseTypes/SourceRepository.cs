using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.Client
{
    /// <summary>
    /// Represents a Server endpoint. Exposes methods to get a specific resource such as Search, Metrics service and so on for the given server endpoint.
    /// </summary>
    public class SourceRepository
    {
        private readonly Dictionary<Type, Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[]> _providerCache;
        private readonly PackageSource _source;

        /// <summary>
        /// Source Repository
        /// </summary>
        /// <param name="source">source url</param>
        /// <param name="providers">Resource providers</param>
        public SourceRepository(PackageSource source, IEnumerable<KeyValuePair<INuGetResourceProviderMetadata, INuGetResourceProvider>> providers)
            : this(source, providers.Select(p => new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => p.Value, p.Key)))
        {
        }

        /// <summary>
        /// Source Repository
        /// </summary>
        /// <param name="source">source url</param>
        /// <param name="providers">Resource providers</param>
        public SourceRepository(PackageSource source, IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers)
            : this()
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (providers == null)
            {
                throw new ArgumentNullException("providers");
            }

            _source = source;
            _providerCache = Init(providers);
        }

        /// <summary>
        /// Internal default constructor
        /// </summary>
        protected SourceRepository()
        {
        }

        /// <summary>
        /// Package source
        /// </summary>
        public virtual PackageSource PackageSource
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
        public virtual T GetResource<T>() where T : class, INuGetResource
        {
            return GetResource<T>(CancellationToken.None);
        }

        /// <summary>
        /// Returns a resource from the SourceRepository if it exists.
        /// </summary>
        /// <typeparam name="T">Expected resource type</typeparam>
        /// <returns>Null if the resource does not exist</returns>
        public virtual T GetResource<T>(CancellationToken token) where T : class, INuGetResource
        {
            Task<T> task = GetResourceAsync<T>(token);
            task.Wait();

            return task.Result;
        }

        /// <summary>
        /// Returns a resource from the SourceRepository if it exists.
        /// </summary>
        /// <typeparam name="T">Expected resource type</typeparam>
        /// <returns>Null if the resource does not exist</returns>
        public virtual async Task<T> GetResourceAsync<T>() where T : class, INuGetResource
        {
            return await GetResourceAsync<T>(CancellationToken.None);
        }

        /// <summary>
        /// Returns a resource from the SourceRepository if it exists.
        /// </summary>
        /// <typeparam name="T">Expected resource type</typeparam>
        /// <returns>Null if the resource does not exist</returns>
        public virtual async Task<T> GetResourceAsync<T>(CancellationToken token) where T : class, INuGetResource
        {
            Type resourceType = typeof(T);
            Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] possible = null;

            if (_providerCache.TryGetValue(resourceType, out possible))
            {
                foreach (var provider in possible)
                {
                    Tuple<bool, INuGetResource> result = await provider.Value.TryCreate(this, token);
                    if (result.Item1)
                    {
                        return (T)result.Item2;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Initialize provider cache
        /// </summary>
        /// <param name="providers"></param>
        /// <returns></returns>
        private static Dictionary<Type, Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[]>
            Init(IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers)
        {
            var cache = new Dictionary<Type, Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[]>();

            foreach (var group in providers.GroupBy(p => p.Metadata.ResourceType))
            {
                cache.Add(group.Key, Sort(group).ToArray());
            }

            return cache;
        }

        // TODO: improve this sort
        private static Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[]
            Sort(IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> group)
        {
            // initial ordering to help make this deterministic
            var items = new List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>(
                group.OrderBy(e => e.Metadata.Name).ThenBy(e => e.Metadata.After.Count()).ThenBy(e => e.Metadata.Before.Count()));

            ProviderComparer comparer = new ProviderComparer();

            var ordered = new Queue<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>();

            // List.Sort does not work when lists have unsolvable gaps, which can occur here
            while (items.Count > 0)
            {
                Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata> best = items[0];

                for (int i = 1; i < items.Count; i++)
                {
                    if (comparer.Compare(items[i], best) < 0)
                    {
                        best = items[i];
                    }
                }

                items.Remove(best);
                ordered.Enqueue(best);
            }

            return ordered.ToArray();
        }
    }
}