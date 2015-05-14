// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Creates/caches IPackageRepositories
    /// </summary>
    public class PackageRepositoryResourceV2Provider : ResourceProvider
    {
        private readonly ConcurrentDictionary<Configuration.PackageSource, PackageRepositoryResourceV2> _cache;

        public PackageRepositoryResourceV2Provider()
            : base(typeof(PackageRepositoryResourceV2), "PackageRepositoryResourceV2Provider", NuGetResourceProviderPositions.Last)
        {
            _cache = new ConcurrentDictionary<Configuration.PackageSource, PackageRepositoryResourceV2>();
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PackageRepositoryResourceV2 repoResource = null;

            if (!_cache.TryGetValue(source.PackageSource, out repoResource))
            {
                IPackageRepository repo = null;

                // check if the source passed in contains the repository
                var v2Source = source.PackageSource as V2PackageSource;

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
                        if (V2Utilities.IsV2(source.PackageSource))
                        {
                            // Get a IPackageRepo object and add it to the cache.
                            repo = V2Utilities.GetV2SourceRepository(source.PackageSource);
                        }
                    }
                    catch
                    {
                        // *TODOs:Do tracing and throw apppropriate exception here.
                        repoResource = null;

                        // For package source that uses relative path, it will throw UriFormat exception and go here.
                        // Comment out Debug.Fail so that functional tests using relative path won't be blocked by the Assertion window.
                        //Debug.Fail("Unable to create V2 repository on: " + source.PackageSource.Source);
                    }
                }

                if (repo != null)
                {
                    repoResource = new PackageRepositoryResourceV2(repo);
                    _cache.TryAdd(source.PackageSource, repoResource);
                }
            }

            return Task.FromResult(Tuple.Create<bool, INuGetResource>(repoResource != null, repoResource));
        }
    }
}
