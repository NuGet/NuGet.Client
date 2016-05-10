// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// A caching source repository provider intended to be used as a singleton.
    /// </summary>
    public class CachingSourceProvider : ISourceRepositoryProvider
    {
        private readonly IPackageSourceProvider _packageSourceProvider;
        private readonly List<Lazy<INuGetResourceProvider>> _resourceProviders
            = new List<Lazy<INuGetResourceProvider>>();
        private readonly List<SourceRepository> _repositories = new List<SourceRepository>();

        // There should only be one instance of the source repository for each package source.
        private readonly ConcurrentDictionary<string, SourceRepository> _cachedSources
            = new ConcurrentDictionary<string, SourceRepository>(StringComparer.OrdinalIgnoreCase);

        public CachingSourceProvider(IPackageSourceProvider packageSourceProvider)
        {
            _packageSourceProvider = packageSourceProvider;

            _resourceProviders.AddRange(FactoryExtensionsV2.GetCoreV3(Repository.Provider));

            _repositories = _packageSourceProvider.LoadPackageSources()
                .Where(s => s.IsEnabled)
                .Select(CreateRepository)
                .ToList();
        }

        /// <summary>
        /// Retrieve repositories that have been cached.
        /// </summary>
        public IEnumerable<SourceRepository> GetRepositories()
        {
            return _repositories;
        }

        /// <summary>
        /// Create a repository and add it to the cache.
        /// </summary>
        public SourceRepository CreateRepository(string source)
        {
            return CreateRepository(new PackageSource(source));
        }

        /// <summary>
        /// Create a repository and add it to the cache.
        /// </summary>
        public SourceRepository CreateRepository(PackageSource source)
        {
            return _cachedSources.GetOrAdd(source.Source, new SourceRepository(source, _resourceProviders));
        }

        public IPackageSourceProvider PackageSourceProvider
        {
            get { return _packageSourceProvider; }
        }
    }
}
