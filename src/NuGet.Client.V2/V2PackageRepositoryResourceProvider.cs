using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    /// <summary>
    /// Creates/caches IPackageRepositories
    /// </summary>
    [NuGetResourceProviderMetadata(typeof(V2PackageRepositoryResource), "V2PackageRepositoryResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2PackageRepositoryResourceProvider : INuGetResourceProvider
    {
        // TODO: make these weak references
        private readonly ConcurrentDictionary<Configuration.PackageSource, V2PackageRepositoryResource> _cache;

        public V2PackageRepositoryResourceProvider()
        {
            _cache = new ConcurrentDictionary<Configuration.PackageSource, V2PackageRepositoryResource>();
        }

        // TODO: clean up
        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V2PackageRepositoryResource repoResource = null;

            if (!_cache.TryGetValue(source.PackageSource, out repoResource))
            {
                IPackageRepository repo = null;

                // check if the source passed in contains the repository
                V2PackageSource v2Source = source.PackageSource as V2PackageSource;

                if (v2Source != null)
                {
                    // special case for some of the remaining legacy areas
                    repo = v2Source.CreatePackageRepository();
                }
                else
                {
                    try
                    {
                        // if it's not in cache, then check if it is V2.
                        if (await V2Utilities.IsV2(source.PackageSource))
                        {
                            // Get a IPackageRepo object and add it to the cache.
                            repo = V2Utilities.GetV2SourceRepository(source.PackageSource);
                        }
                    }
                    catch (Exception)
                    {
                        // *TODOs:Do tracing and throw apppropriate exception here.
                        repoResource = null;

                        Debug.Fail("Unable to create V2 repository on: " + source.PackageSource.Source);
                    }
                }

                if (repo != null)
                {
                    repoResource = new V2PackageRepositoryResource(repo);
                    _cache.TryAdd(source.PackageSource, repoResource);
                }
            }

            return Tuple.Create<bool, INuGetResource>(repoResource != null, repoResource);
        }
    }
}
