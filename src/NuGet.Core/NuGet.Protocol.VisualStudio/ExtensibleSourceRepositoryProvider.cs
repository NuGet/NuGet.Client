// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
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

        // TODO: add support for reloading sources when changes occur
        private readonly Configuration.IPackageSourceProvider _packageSourceProvider;
        private IEnumerable<Lazy<INuGetResourceProvider>> _resourceProviders;
        private Lazy<List<SourceRepository>> _repositories;

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
            : this(new Configuration.PackageSourceProvider(settings, migratePackageSources: null), resourceProviders)
        {
        }

        /// <summary>
        /// Non-MEF constructor
        /// </summary>
        public ExtensibleSourceRepositoryProvider(Configuration.IPackageSourceProvider packageSourceProvider, IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders)
        {
            _packageSourceProvider = packageSourceProvider;
            _resourceProviders = Repository.Provider.GetVisualStudio().Concat(resourceProviders);

            // Hook up event to refresh package sources when the package sources changed
            packageSourceProvider.PackageSourcesChanged += (sender, e) => { ResetRepositories(); };
        }

        /// <summary>
        /// Retrieve repositories
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SourceRepository> GetRepositories()
        {
            if (_repositories == null)
            {
                // initialize repositories from package source 
                ResetRepositories();
            }

            return _repositories.Value;
        }

        /// <summary>
        /// Create a repository for one time use.
        /// </summary>
        public SourceRepository CreateRepository(PackageSource source)
        {
            return CreateRepository(source, FeedType.Undefined);
        }

        /// <summary>
        /// Create a repository for one time use.
        /// </summary>
        public SourceRepository CreateRepository(PackageSource source, FeedType type)
        {
            return new SourceRepository(source, _resourceProviders, type);
        }

        public Configuration.IPackageSourceProvider PackageSourceProvider
        {
            get { return _packageSourceProvider; }
        }

        private void ResetRepositories()
        {
            // initialize it lazy since it doesn't impact RPS test performance and evaluate
            // only when somebody reads repositories value.
            _repositories = new Lazy<List<SourceRepository>>(GetRepositoriesCore);
        }

        private List<SourceRepository> GetRepositoriesCore()
        {
            var repositories = new List<SourceRepository>();
            foreach (var source in _packageSourceProvider.LoadPackageSources())
            {
                if (source.IsEnabled)
                {
                    var sourceRepo = new SourceRepository(source, _resourceProviders);
                    repositories.Add(sourceRepo);
                }
            }

            return repositories;
        }
    }
}