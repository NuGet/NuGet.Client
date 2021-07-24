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
using NuGet.Shared;

namespace NuGet.DependencyResolver
{
    public class RemoteWalkContext
    {
        public RemoteWalkContext(SourceCacheContext cacheContext, PackageNamespacesConfiguration packageNamespaces, ILogger logger)
        {
            CacheContext = cacheContext ?? throw new ArgumentNullException(nameof(cacheContext));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ProjectLibraryProviders = new List<IDependencyProvider>();
            LocalLibraryProviders = new List<IRemoteDependencyProvider>();
            RemoteLibraryProviders = new List<IRemoteDependencyProvider>();
            PackageNamespaces = packageNamespaces ?? throw new ArgumentNullException(nameof(packageNamespaces));

            FindLibraryEntryCache = new ConcurrentDictionary<LibraryRangeCacheKey, Task<GraphItem<RemoteResolveResult>>>();

            LockFileLibraries = new Dictionary<LockFileCacheKey, IList<LibraryIdentity>>();
        }

        public SourceCacheContext CacheContext { get; }
        public ILogger Logger { get; }
        public IList<IDependencyProvider> ProjectLibraryProviders { get; }
        public IList<IRemoteDependencyProvider> LocalLibraryProviders { get; }
        public IList<IRemoteDependencyProvider> RemoteLibraryProviders { get; }
        public PackageNamespacesConfiguration PackageNamespaces { get; }

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

        /// <summary>
        /// Applies Package namespace filtering for a given package
        /// </summary>
        /// <param name="libraryRange"></param>
        /// <returns>Returns a subset of sources when namespaces are configured otherwise returns all the sources</returns>
        public IList<IRemoteDependencyProvider> FilterDependencyProvidersForLibrary(LibraryRange libraryRange)
        {
            if (libraryRange == default)
                throw new ArgumentNullException(nameof(libraryRange));

            // filter package namespaces if enabled            
            if (PackageNamespaces?.AreNamespacesEnabled == true && libraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package))
            {
                IReadOnlyList<string> sources = PackageNamespaces.GetConfiguredPackageSources(libraryRange.Name);

                if (sources == null || sources.Count == 0)
                {
                    return Array.Empty<IRemoteDependencyProvider>();
                }

                return RemoteLibraryProviders.Where(p => sources.Contains(p.Source.Source)).AsList();
            }
            return RemoteLibraryProviders;
        }
    }
}
