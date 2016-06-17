// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Implements a consolidated metadata provider for multiple package sources 
    /// with optional local repository as a fallback metadata source.
    /// </summary>
    internal class MultiSourcePackageMetadataProvider : IPackageMetadataProvider
    {
        private readonly IEnumerable<SourceRepository> _sourceRepositories;
        private readonly SourceRepository _localRepository;
        private readonly IEnumerable<SourceRepository> _globalLocalRepositories;
        private readonly Common.ILogger _logger;

        public MultiSourcePackageMetadataProvider(
            IEnumerable<SourceRepository> sourceRepositories,
            SourceRepository optionalLocalRepository,
            IEnumerable<SourceRepository> optionalGlobalLocalRepositories,
            Common.ILogger logger)
        {
            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }
            _sourceRepositories = sourceRepositories;

            _localRepository = optionalLocalRepository;

            _globalLocalRepositories = optionalGlobalLocalRepositories;

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            _logger = logger;
        }

        public async Task<IPackageSearchMetadata> GetPackageMetadataAsync(PackageIdentity identity, bool includePrerelease, CancellationToken cancellationToken)
        {
            var tasks = _sourceRepositories
                .Select(r => r.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken))
                .ToList();

            if (_localRepository != null)
            {
                tasks.Add(_localRepository.GetPackageMetadataFromLocalSourceAsync(identity, cancellationToken));
            }

            var ignored = tasks
                .Select(task => task.ContinueWith(LogError, TaskContinuationOptions.OnlyOnFaulted))
                .ToArray();

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            var master = completed.FirstOrDefault(m => !string.IsNullOrEmpty(m.Summary))
                ?? completed.FirstOrDefault()
                ?? PackageSearchMetadataBuilder.FromIdentity(identity).Build();

            return master.WithVersions(
                asyncValueFactory: () => MergeVersionsAsync(identity, completed));
        }

        public async Task<IPackageSearchMetadata> GetLatestPackageMetadataAsync(PackageIdentity identity,
            bool includePrerelease, CancellationToken cancellationToken)
        {
            var tasks = _sourceRepositories
                .Select(r => r.GetLatestPackageMetadataAsync(identity.Id, includePrerelease, cancellationToken))
                .ToArray();

            var ignored = tasks
                .Select(task => task.ContinueWith(LogError, TaskContinuationOptions.OnlyOnFaulted))
                .ToArray();

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            var highest = completed
                .OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease)
                .FirstOrDefault();

            return highest?.WithVersions(
                asyncValueFactory: () => MergeVersionsAsync(identity, completed));
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> GetPackageMetadataListAsync(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken cancellationToken)
        {
            var tasks = _sourceRepositories
                .Select(r => r.GetPackageMetadataListAsync(packageId, includePrerelease, includeUnlisted, cancellationToken))
                .ToArray();

            var ignored = tasks
                .Select(task => task.ContinueWith(LogError, TaskContinuationOptions.OnlyOnFaulted))
                .ToArray();

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            return completed.SelectMany(p => p);
        }

        /// <summary>
        /// Get package metadata from the package folders.
        /// </summary>
        public async Task<IPackageSearchMetadata> GetLocalPackageMetadataAsync(
            PackageIdentity identity,
            bool includePrerelease,
            CancellationToken cancellationToken)
        {
            var sources = new List<SourceRepository>();

            if (_localRepository != null)
            {
                sources.Add(_localRepository);
            }

            if (_globalLocalRepositories != null)
            {
                sources.AddRange(_globalLocalRepositories);
            }

            // Take the package from the first source it is found in
            foreach (var source in sources)
            {
                var result = await source.GetPackageMetadataFromLocalSourceAsync(identity, cancellationToken);

                if (result != null)
                {
                    return result.WithVersions(asyncValueFactory: 
                        () => FetchAndMergeVersionsAsync(identity, includePrerelease, cancellationToken));
                }
            }

            return null;
        }

        private static async Task<IEnumerable<VersionInfo>> MergeVersionsAsync(PackageIdentity identity, IEnumerable<IPackageSearchMetadata> packages)
        {
            var versions = await Task.WhenAll(packages.Select(m => m.GetVersionsAsync()));

            var allVersions = versions
                .SelectMany(v => v)
                .Concat(new[] { new VersionInfo(identity.Version) });

            return allVersions
                .GroupBy(v => v.Version)
                .Select(g => g.OrderBy(v => v.DownloadCount).First())
                .ToArray();
        }

        private async Task<IEnumerable<VersionInfo>> FetchAndMergeVersionsAsync(PackageIdentity identity, bool includePrerelease, CancellationToken cancellationToken)
        {
            var tasks = _sourceRepositories
                .Select(r => r.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken))
                .ToList();

            if (_localRepository != null)
            {
                tasks.Add(_localRepository.GetPackageMetadataFromLocalSourceAsync(identity, cancellationToken));
            }

            var ignored = tasks
                .Select(task => task.ContinueWith(LogError, TaskContinuationOptions.OnlyOnFaulted))
                .ToArray();

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            return await MergeVersionsAsync(identity, completed);
        }

        private void LogError(Task task)
        {
            foreach (var ex in task.Exception.Flatten().InnerExceptions)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }
}
