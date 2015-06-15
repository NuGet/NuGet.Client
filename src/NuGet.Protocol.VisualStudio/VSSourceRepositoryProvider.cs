// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// SourceRepositoryProvider is the high level source for repository objects representing package sources.
    /// </summary>
    [Export(typeof(ISourceRepositoryProvider))]
    public sealed class ExtensibleSourceRepositoryProvider : ISourceRepositoryProvider
    {
        private static Configuration.PackageSource[] DefaultPrimarySources = new[]
            {
                new Configuration.PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName, isEnabled: true, isOfficial: true)
                    {
                        Description = Strings.v3sourceDescription,
                        ProtocolVersion = 3
                    }
            };

        private static Configuration.PackageSource[] DefaultSecondarySources = new[]
            {
                new Configuration.PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName, isEnabled: true, isOfficial: true)
                    {
                        Description = Strings.v2sourceDescription,
                        ProtocolVersion = 2
                    }
            };

        // TODO: add support for reloading sources when changes occur
        private readonly Configuration.IPackageSourceProvider _packageSourceProvider;
        private IEnumerable<Lazy<INuGetResourceProvider>> _resourceProviders;
        private List<SourceRepository> _repositories;

        /// <summary>
        /// Public parameter-less constructor for SourceRepositoryProvider
        /// </summary>
        public ExtensibleSourceRepositoryProvider()
        {
        }

        /// <summary>
        /// Public importing constructor for SourceRepositoryProvider
        /// </summary>
        [ImportingConstructor]
        public ExtensibleSourceRepositoryProvider([ImportMany] IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders, [Import] Configuration.ISettings settings)
            : this(new Configuration.PackageSourceProvider(settings, DefaultPrimarySources, DefaultSecondarySources, migratePackageSources: null), resourceProviders)
        {
        }

        /// <summary>
        /// Non-MEF constructor
        /// </summary>
        public ExtensibleSourceRepositoryProvider(Configuration.IPackageSourceProvider packageSourceProvider, IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders)
        {
            _packageSourceProvider = packageSourceProvider;
            _resourceProviders = Repository.Provider.GetVisualStudio().Concat(resourceProviders);
            _repositories = new List<SourceRepository>();

            // Refresh the package sources
            Init();

            // Hook up event to refresh package sources when the package sources changed
            packageSourceProvider.PackageSourcesChanged += (sender, e) => { Init(); };
        }

        /// <summary>
        /// Retrieve repositories
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SourceRepository> GetRepositories()
        {
            return _repositories;
        }

        /// <summary>
        /// Create a repository for one time use.
        /// </summary>
        public SourceRepository CreateRepository(Configuration.PackageSource source)
        {
            return new SourceRepository(source, _resourceProviders);
        }

        public Configuration.IPackageSourceProvider PackageSourceProvider
        {
            get { return _packageSourceProvider; }
        }

        private void Init()
        {
            _repositories.Clear();
            foreach (var source in _packageSourceProvider.LoadPackageSources())
            {
                if (source.IsEnabled)
                {
                    var sourceRepo = new SourceRepository(source, _resourceProviders);
                    _repositories.Add(sourceRepo);
                }
            }
        }
    }
}
