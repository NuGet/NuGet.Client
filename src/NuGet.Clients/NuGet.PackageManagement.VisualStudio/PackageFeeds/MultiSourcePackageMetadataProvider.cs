// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Implements a consolidated metadata provider for multiple package sources 
    /// with optional local repository as a fallback metadata source.
    /// </summary>
    public sealed class MultiSourcePackageMetadataProvider : IPackageMetadataProvider
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
                .Select(r => GetMetadataTaskSafeAsync(() => r.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken)))
                .ToList();

            if (_localRepository != null)
            {
                tasks.Add(_localRepository.GetPackageMetadataFromLocalSourceAsync(identity, cancellationToken));
            }

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            var master = completed.FirstOrDefault(m => !string.IsNullOrEmpty(m.Summary))
                ?? completed.FirstOrDefault()
                ?? PackageSearchMetadataBuilder.FromIdentity(identity).Build();

            return master.WithVersions(
                asyncValueFactory: () => MergeVersionsAsync(identity, completed));
        }

        public async Task<IPackageSearchMetadata> GetLatestPackageMetadataAsync(
            PackageIdentity identity,
            NuGetProject project,
            bool includePrerelease, 
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // get all package references for all the projects and cache locally
            var packageReferences = await project.GetInstalledPackagesAsync(cancellationToken);

            // filter package references for current package identity
            var matchedPackageReferences = packageReferences
                .Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.PackageIdentity.Id, identity.Id));

            cancellationToken.ThrowIfCancellationRequested();
            // Allowed version range for current package across all selected projects
            // Picks the first non-default range
            var allowedVersions = matchedPackageReferences
                .Select(r => r.AllowedVersions)
                .FirstOrDefault(v => v != null) ?? VersionRange.All;

            cancellationToken.ThrowIfCancellationRequested();
            var tasks = _sourceRepositories
                .Select(r => GetMetadataTaskSafeAsync(() => r.GetLatestPackageMetadataAsync(identity.Id, includePrerelease, cancellationToken, allowedVersions)))
                .ToArray();

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            cancellationToken.ThrowIfCancellationRequested();
            var highest = completed
                .OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease)
                .FirstOrDefault();

            cancellationToken.ThrowIfCancellationRequested();
            return highest?.WithVersions(
                asyncValueFactory: () => MergeVersionsAsync(identity, completed));
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> GetPackageMetadataListAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            var tasks = _sourceRepositories
                .Select(r => GetMetadataTaskSafeAsync(()=> r.GetPackageMetadataListAsync(packageId, includePrerelease, includeUnlisted, cancellationToken)))
                .ToArray();

            var completed = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            var packages = completed.SelectMany(p => p);

            var uniquePackages = packages
                .GroupBy(
                    m => m.Identity.Version,
                    (v, ms) => ms.First());

            return uniquePackages;
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
                    var versionsAndDeprecationMetadataTask = FetchAndMergeVersionsAndDeprecationMetadataAsync(identity, includePrerelease, cancellationToken);

                    return PackageSearchMetadataBuilder
                        .FromMetadata(result)
                        .WithVersions(AsyncLazy.New(async () => (await versionsAndDeprecationMetadataTask).versions))
                        .WithDeprecation(AsyncLazy.New(async () => (await versionsAndDeprecationMetadataTask).deprecationMetadata))
                        .Build();
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

        private static async Task<PackageDeprecationMetadata> MergeDeprecationMetadataAsync(PackageIdentity identity, IEnumerable<IPackageSearchMetadata> packages)
        {
            var deprecationMetadatas = await Task.WhenAll(packages.Select(m => m.GetDeprecationMetadataAsync()));
            return deprecationMetadatas.FirstOrDefault(d => d != null);
        }

        private async Task<(IEnumerable<VersionInfo> versions, PackageDeprecationMetadata deprecationMetadata)> FetchAndMergeVersionsAndDeprecationMetadataAsync(
            PackageIdentity identity, bool includePrerelease, CancellationToken cancellationToken)
        {
            var tasks = _sourceRepositories
                .Select(r => GetMetadataTaskSafeAsync(
                    () => r.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken)))
                .ToList();

            if (_localRepository != null)
            {
                tasks.Add(_localRepository.GetPackageMetadataFromLocalSourceAsync(identity, cancellationToken));
            }

            var metadatas = (await Task.WhenAll(tasks))
                .Where(m => m != null);

            return (await MergeVersionsAsync(identity, metadatas), await MergeDeprecationMetadataAsync(identity, metadatas));
        }

        private async Task<T> GetMetadataTaskSafeAsync<T>(Func<Task<T>> getMetadataTask) where T: class
        {
            try
            {
                return await getMetadataTask();
            }
            //catch (OperationCanceledException)
            //{
            //    throw;
            //}
            catch (Exception e)
            {
                LogError(e);
            }
            return null;
        }

        private void LogError(Exception exception)
        {
            _logger.LogError(ExceptionUtilities.DisplayMessage(exception));
        }
    }
}
