﻿using System;
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
        private readonly Logging.ILogger _logger;

        public MultiSourcePackageMetadataProvider(
            IEnumerable<SourceRepository> sourceRepositories,
            SourceRepository optionalLocalRepository,
            Logging.ILogger logger)
        {
            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }
            _sourceRepositories = sourceRepositories;

            _localRepository = optionalLocalRepository;

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
                .Select(r => r.GetLatestPackageMetadataAsync(identity.Id, includePrerelease, cancellationToken));

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
                .Select(r => r.GetPackageMetadataListAsync(packageId, includePrerelease, includeUnlisted, cancellationToken));

            var ignored = tasks
                .Select(task => task.ContinueWith(LogError, TaskContinuationOptions.OnlyOnFaulted))
                .ToArray();

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            return completed.SelectMany(p => p);
        }

        private static async Task<IEnumerable<VersionInfo>> MergeVersionsAsync(PackageIdentity identity, IEnumerable<IPackageSearchMetadata> packages)
        {
            var versions = await Task.WhenAll(packages.Select(m => m.GetVersionsAsync()));

            var allVersions = versions
                .SelectMany(v => v)
                .Concat(new[] { new VersionInfo(identity.Version) });

            return allVersions
                .GroupBy(v => v.Version, v => v.DownloadCount)
                .Select(g => new VersionInfo(g.Key, g.Max()))
                .ToArray();
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
