// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.DependencyResolver
{
    public class RemoteWalkContext
    {
        public RemoteWalkContext(SourceCacheContext cacheContext, ILogger logger)
        {
            CacheContext = cacheContext ?? throw new ArgumentNullException(nameof(cacheContext));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ProjectLibraryProviders = new List<IDependencyProvider>();
            LocalLibraryProviders = new List<IRemoteDependencyProvider>();
            RemoteLibraryProviders = new List<IRemoteDependencyProvider>();

            FindLibraryEntryCache = new ConcurrentDictionary<LibraryRangeCacheKey, Task<GraphItem<RemoteResolveResult>>>();

            LockFileLibraries = new Dictionary<LockFileCacheKey, IList<LibraryIdentity>>();
        }

        public SourceCacheContext CacheContext { get; }
        public ILogger Logger { get; }

        public IList<IDependencyProvider> ProjectLibraryProviders { get; }
        public IList<IRemoteDependencyProvider> LocalLibraryProviders { get; }
        public IList<IRemoteDependencyProvider> RemoteLibraryProviders { get; }
        public PackageNamespacesConfiguration PackageNamespaces { get; set; }

        /// <summary>
        /// Packages lock file libraries to be used while generating restore graph.
        /// </summary>
        public IDictionary<LockFileCacheKey, IList<LibraryIdentity>> LockFileLibraries { get; }

        /// <summary>
        /// Library entry cache.
        /// </summary>
        public ConcurrentDictionary<LibraryRangeCacheKey, Task<GraphItem<RemoteResolveResult>>> FindLibraryEntryCache { get; }

        /// <summary>
        /// True if this is a csproj or similar project. Xproj should be false.
        /// </summary>
        public bool IsMsBuildBased { get; set; }

        public IList<IRemoteDependencyProvider> FilteredRemoteLibraryProviders(LibraryRange libraryRange)
        {
            // filter package namespaces if enabled            
            if (libraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package) && PackageNamespaces?.AreNamespacesEnabled == true)
            {
                IReadOnlyList<string> sources = PackageNamespaces.GetConfiguredPackageSources(libraryRange.Name);

                if (sources == null || sources.Count == 0)
                    throw new Exception("something went wrong in namespaces work");

                return RemoteLibraryProviders.Where(p => sources.Contains(p.Source.Name)).ToList();
            }
            return RemoteLibraryProviders;
        }
    }
}
