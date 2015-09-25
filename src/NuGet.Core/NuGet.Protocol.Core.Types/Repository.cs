// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.Protocol.Core.Types
{
    public static class Repository
    {
        public static RepositoryFactory Factory
        {
            get { return new RepositoryFactory(); }
        }

        public static ProviderFactory Provider
        {
            get { return new ProviderFactory(); }
        }

        public class RepositoryFactory
        {
            // Methods are added by extension
        }

        public class ProviderFactory
        {
            // Methods are added by extension
        }

        /// <summary>
        /// Create the default source repository provider
        /// </summary>
        public static ISourceRepositoryProvider CreateProvider(IEnumerable<INuGetResourceProvider> resourceProviders)
        {
            return new SourceRepositoryProvider(Settings.LoadDefaultSettings(null, null, null), CreateLazy(resourceProviders));
        }

        /// <summary>
        /// Find sources from nuget.config based on the root path
        /// </summary>
        /// <param name="rootPath">lowest folder path</param>
        public static ISourceRepositoryProvider CreateProvider(IEnumerable<INuGetResourceProvider> resourceProviders, string rootPath)
        {
            return new SourceRepositoryProvider(Settings.LoadDefaultSettings(rootPath, null, null), CreateLazy(resourceProviders));
        }

        /// <summary>
        /// Create a source provider for the given sources
        /// </summary>
        public static ISourceRepositoryProvider CreateProvider(IEnumerable<INuGetResourceProvider> resourceProviders, IEnumerable<string> sources)
        {
            return CreateProvider(resourceProviders, sources.Select(s => new PackageSource(s)));
        }

        /// <summary>
        /// Create a source provider for the given sources and with the extra providers.
        /// </summary>
        public static ISourceRepositoryProvider CreateProvider(IEnumerable<INuGetResourceProvider> resourceProviders, IEnumerable<PackageSource> sources)
        {
            if (sources == null)
            {
                throw new ArgumentNullException("sources");
            }

            if (resourceProviders == null)
            {
                throw new ArgumentNullException("resourceProviders");
            }

            var sourceProvider = new PackageSourceProvider(NullSettings.Instance, sources, Enumerable.Empty<PackageSource>());

            return new SourceRepositoryProvider(sourceProvider, CreateLazy(resourceProviders));
        }

        /// <summary>
        /// Create a SourceRepository
        /// </summary>
        public static SourceRepository CreateSource(IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders, string sourceUrl)
        {
            return CreateSource(resourceProviders, new PackageSource(sourceUrl));
        }

        /// <summary>
        /// Create a SourceRepository
        /// </summary>
        public static SourceRepository CreateSource(IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders, PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (resourceProviders == null)
            {
                throw new ArgumentNullException("resourceProviders");
            }

            return new SourceRepository(source, resourceProviders);
        }

        private static IEnumerable<Lazy<INuGetResourceProvider>> CreateLazy(IEnumerable<INuGetResourceProvider> providers)
        {
            return providers.Select(e => new Lazy<INuGetResourceProvider>(() => e));
        }
    }
}
