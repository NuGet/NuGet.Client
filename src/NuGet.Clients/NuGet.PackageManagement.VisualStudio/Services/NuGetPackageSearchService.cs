// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class NuGetPackageSearchService : INuGetSearchService
    {
        private readonly ServiceActivationOptions _options;
        private readonly IServiceBroker _serviceBroker;
        private readonly AuthorizationServiceClient _authorizationServiceClient;
        private SearchObject? _searchObject;
        private readonly ISharedServiceState _sharedServiceState;
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<SourceRepository> _packagesFolderLocalRepositoryLazy;
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<IReadOnlyList<SourceRepository>> _globalPackageFolderRepositoriesLazy;
        // internal for testing purposes only
        internal readonly static MemoryCache PackageSearchMetadataMemoryCache = new MemoryCache("PackageSearchMetadata",
            new NameValueCollection
            {
                { "cacheMemoryLimitMegabytes", "4" },
                { "physicalMemoryLimitPercentage", "0" },
                { "pollingInterval", "00:02:00" }
            });

        public NuGetPackageSearchService(ServiceActivationOptions options, IServiceBroker sb, AuthorizationServiceClient ac, ISharedServiceState state)
        {
            _options = options;
            _serviceBroker = sb;
            _authorizationServiceClient = ac;
            _sharedServiceState = state;

            _packagesFolderLocalRepositoryLazy = new Microsoft.VisualStudio.Threading.AsyncLazy<SourceRepository>(
                GetPackagesFolderSourceRepositoryAsync,
                NuGetUIThreadHelper.JoinableTaskFactory);
            _globalPackageFolderRepositoriesLazy = new Microsoft.VisualStudio.Threading.AsyncLazy<IReadOnlyList<SourceRepository>>(
                GetGlobalPackageFolderRepositoriesAsync,
                NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public async ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetAllPackagesAsync(
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            IReadOnlyCollection<string> targetFrameworks,
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectContextInfos);
            Assumes.NotNullOrEmpty(packageSources);
            Assumes.NotNull(searchFilter);

            bool recommendPackages = false;
            IReadOnlyCollection<SourceRepository> sourceRepositories = await _sharedServiceState.GetRepositoriesAsync(packageSources, cancellationToken);
            (IPackageFeed? mainFeed, IPackageFeed? recommenderFeed) packageFeeds = await CreatePackageFeedAsync(
                projectContextInfos,
                targetFrameworks,
                itemFilter,
                recommendPackages,
                sourceRepositories,
                cancellationToken);

            Assumes.NotNull(packageFeeds.mainFeed);

            SourceRepository packagesFolderSourceRepository = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
            IEnumerable<SourceRepository> globalPackageFolderRepositories = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);
            var metadataProvider = new MultiSourcePackageMetadataProvider(
                sourceRepositories,
                packagesFolderSourceRepository,
                globalPackageFolderRepositories,
                new VisualStudioActivityLogger());

            var searchObject = new SearchObject(packageFeeds.mainFeed, packageFeeds.recommenderFeed, metadataProvider, packageSources, PackageSearchMetadataMemoryCache);
            return await searchObject.GetAllPackagesAsync(searchFilter, cancellationToken);
        }

        public async ValueTask<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo?)> GetPackageMetadataAsync(
            PackageIdentity identity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(identity);
            Assumes.NotNullOrEmpty(packageSources);

            string cacheId = PackageSearchMetadataCacheItem.GetCacheId(identity.Id, includePrerelease, packageSources);
            if (PackageSearchMetadataMemoryCache.Get(cacheId) is PackageSearchMetadataCacheItem backgroundDataCache)
            {
                PackageSearchMetadataCacheItemEntry cacheItem = await backgroundDataCache.GetPackageSearchMetadataCacheVersionedItemAsync(identity, cancellationToken);
                PackageSearchMetadataContextInfo packageSearchData = await cacheItem.DetailedPackageSearchMetadataContextInfo;
                PackageDeprecationMetadataContextInfo? deprecatedData = await cacheItem.PackageDeprecationMetadataContextInfo;
                return (packageSearchData, deprecatedData);
            }

            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IPackageSearchMetadata packageMetadata = await packageMetadataProvider.GetPackageMetadataAsync(
                identity,
                includePrerelease,
                cancellationToken);

            PackageSearchMetadataContextInfo packageSearchMetadataContextInfo = PackageSearchMetadataContextInfo.Create(packageMetadata);
            PackageDeprecationMetadataContextInfo? deprecationMetadataContextInfo = null;

            PackageDeprecationMetadata? deprecationMetadata = await packageMetadata.GetDeprecationMetadataAsync();
            if (deprecationMetadata != null)
            {
                deprecationMetadataContextInfo = PackageDeprecationMetadataContextInfo.Create(deprecationMetadata);
            }

            return (packageSearchMetadataContextInfo, deprecationMetadataContextInfo);
        }

        public async ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetPackageMetadataListAsync(
            string id,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(id);
            Assumes.NotNullOrEmpty(packageSources);

            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IEnumerable<IPackageSearchMetadata> packageMetadata = await packageMetadataProvider.GetPackageMetadataListAsync(
                id,
                includePrerelease,
                includeUnlisted,
                cancellationToken);

            return packageMetadata.Select(package => PackageSearchMetadataContextInfo.Create(package)).ToList();
        }

        public async ValueTask<IReadOnlyCollection<VersionInfoContextInfo>> GetPackageVersionsAsync(
            PackageIdentity identity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(identity);
            Assumes.NotNullOrEmpty(packageSources);

            string cacheId = PackageSearchMetadataCacheItem.GetCacheId(identity.Id, includePrerelease, packageSources);
            PackageSearchMetadataCacheItem? backgroundDataCache = PackageSearchMetadataMemoryCache.Get(cacheId) as PackageSearchMetadataCacheItem;
            if (backgroundDataCache != null)
            {
                return await backgroundDataCache.AllVersionsContextInfo;
            }

            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IPackageSearchMetadata packageMetadata = await packageMetadataProvider.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
            IEnumerable<VersionInfo> versions = await packageMetadata.GetVersionsAsync();

            return await Task.WhenAll(versions.Select(v => VersionInfoContextInfo.CreateAsync(v).AsTask()));
        }

        public async ValueTask<PackageDeprecationMetadataContextInfo?> GetDeprecationMetadataAsync(
            PackageIdentity identity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(identity);
            Assumes.NotNullOrEmpty(packageSources);

            string cacheId = PackageSearchMetadataCacheItem.GetCacheId(identity.Id, includePrerelease, packageSources);
            PackageSearchMetadataCacheItem? backgroundDataCache = PackageSearchMetadataMemoryCache.Get(cacheId) as PackageSearchMetadataCacheItem;
            if (backgroundDataCache != null)
            {
                PackageSearchMetadataCacheItemEntry cacheItem = await backgroundDataCache.GetPackageSearchMetadataCacheVersionedItemAsync(identity, cancellationToken);
                return await cacheItem.PackageDeprecationMetadataContextInfo;
            }

            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IPackageSearchMetadata packageMetadata = await packageMetadataProvider.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
            PackageDeprecationMetadata deprecationMetadata = await packageMetadata.GetDeprecationMetadataAsync();
            if (deprecationMetadata == null)
            {
                return null;
            }
            return PackageDeprecationMetadataContextInfo.Create(deprecationMetadata);
        }

        public async ValueTask<SearchResultContextInfo> RefreshSearchAsync(CancellationToken cancellationToken)
        {
            Assumes.NotNull(_searchObject);
            return await _searchObject.RefreshSearchAsync(cancellationToken);
        }

        public async ValueTask<SearchResultContextInfo> ContinueSearchAsync(CancellationToken cancellationToken)
        {
            Assumes.NotNull(_searchObject);
            return await _searchObject.ContinueSearchAsync(cancellationToken);
        }

        public async ValueTask<SearchResultContextInfo> SearchAsync(
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            IReadOnlyCollection<string> targetFrameworks,
            string searchText,
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            bool useRecommender,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectContextInfos);
            Assumes.NotNullOrEmpty(packageSources);
            Assumes.NotNull(searchFilter);

            IReadOnlyCollection<SourceRepository>? sourceRepositories = await _sharedServiceState.GetRepositoriesAsync(packageSources, cancellationToken);
            (IPackageFeed? mainFeed, IPackageFeed? recommenderFeed) = await CreatePackageFeedAsync(
                projectContextInfos,
                targetFrameworks,
                itemFilter,
                useRecommender,
                sourceRepositories,
                cancellationToken);
            Assumes.NotNull(mainFeed);

            SourceRepository packagesFolderSourceRepository = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
            IEnumerable<SourceRepository> globalPackageFolderRepositories = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);
            var metadataProvider = new MultiSourcePackageMetadataProvider(
                sourceRepositories,
                packagesFolderSourceRepository,
                globalPackageFolderRepositories,
                new VisualStudioActivityLogger());

            _searchObject = new SearchObject(mainFeed, recommenderFeed, metadataProvider, packageSources, PackageSearchMetadataMemoryCache);
            return await _searchObject.SearchAsync(searchText, searchFilter, useRecommender, cancellationToken);
        }

        public async ValueTask<int> GetTotalCountAsync(
            int maxCount,
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            IReadOnlyCollection<string> targetFrameworks,
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectContextInfos);
            Assumes.NotNullOrEmpty(packageSources);
            Assumes.NotNull(searchFilter);

            IReadOnlyCollection<SourceRepository>? sourceRepositories = await _sharedServiceState.GetRepositoriesAsync(packageSources, cancellationToken);
            (IPackageFeed? mainFeed, IPackageFeed? recommenderFeed) = await CreatePackageFeedAsync(projectContextInfos, targetFrameworks, itemFilter, recommendPackages: false, sourceRepositories, cancellationToken);
            Assumes.NotNull(mainFeed);

            SourceRepository packagesFolderSourceRepository = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
            IEnumerable<SourceRepository> globalPackageFolderRepositories = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);
            var metadataProvider = new MultiSourcePackageMetadataProvider(
                sourceRepositories,
                packagesFolderSourceRepository,
                globalPackageFolderRepositories,
                new VisualStudioActivityLogger());

            var searchObject = new SearchObject(mainFeed, recommenderFeed, metadataProvider, packageSources, searchCache: null);
            return await searchObject.GetTotalCountAsync(maxCount, searchFilter, cancellationToken);
        }

        public void Dispose()
        {
            _authorizationServiceClient.Dispose();
        }

        private async ValueTask<IPackageMetadataProvider> GetPackageMetadataProviderAsync(
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<SourceRepository> sourceRepositories = await _sharedServiceState.GetRepositoriesAsync(packageSources, cancellationToken);
            SourceRepository localRepo = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
            IEnumerable<SourceRepository> globalRepo = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);
            return new MultiSourcePackageMetadataProvider(sourceRepositories, localRepo, globalRepo, new VisualStudioActivityLogger());
        }

        private async ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>> GetAllInstalledPackagesAsync(IReadOnlyCollection<IProjectContextInfo> projectContextInfos, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, IReadOnlyCollection<IPackageReferenceContextInfo>>? packageReferences =
                await projectContextInfos.GetInstalledPackagesAsync(_serviceBroker, cancellationToken);
            return packageReferences.SelectMany(e => e.Value).ToList().AsReadOnly();
        }

        private async ValueTask<IInstalledAndTransitivePackages> GetInstalledAndTransitivePackagesAsync(IReadOnlyCollection<IProjectContextInfo> projectContextInfos, CancellationToken cancellationToken)
        {
            IEnumerable<Task<IInstalledAndTransitivePackages>> tasks = projectContextInfos
                .Select(project => project.GetInstalledAndTransitivePackagesAsync(_serviceBroker, cancellationToken).AsTask());
            IInstalledAndTransitivePackages[] installedAndTransitivePackagesArray = await Task.WhenAll(tasks);
            if (installedAndTransitivePackagesArray.Length == 1)
            {
                return installedAndTransitivePackagesArray[0];
            }
            else if (installedAndTransitivePackagesArray.Length > 1)
            {
                List<IPackageReferenceContextInfo> installedPackages = new List<IPackageReferenceContextInfo>();
                List<IPackageReferenceContextInfo> transitivePackages = new List<IPackageReferenceContextInfo>();
                foreach (var installedAndTransitivePackages in installedAndTransitivePackagesArray)
                {
                    installedPackages.AddRange(installedAndTransitivePackages.InstalledPackages);
                    transitivePackages.AddRange(installedAndTransitivePackages.TransitivePackages);
                }
                InstalledAndTransitivePackages collectAllPackagesHere = new InstalledAndTransitivePackages(installedPackages, transitivePackages);
                return collectAllPackagesHere;
            }
            else
            {
                return new InstalledAndTransitivePackages(new List<IPackageReferenceContextInfo>(), new List<IPackageReferenceContextInfo>());
            }
        }

        private async Task<(IPackageFeed? mainFeed, IPackageFeed? recommenderFeed)> CreatePackageFeedAsync(
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<string> targetFrameworks,
            ItemFilter itemFilter,
            bool recommendPackages,
            IEnumerable<SourceRepository> sourceRepositories,
            CancellationToken cancellationToken)
        {
            var logger = new VisualStudioActivityLogger();
            var uiLogger = ServiceLocator.GetInstance<INuGetUILogger>();
            var packageFeeds = (mainFeed: (IPackageFeed?)null, recommenderFeed: (IPackageFeed?)null);

            if (itemFilter == ItemFilter.All && recommendPackages == false)
            {
                packageFeeds.mainFeed = new MultiSourcePackageFeed(sourceRepositories, uiLogger, TelemetryActivity.NuGetTelemetryService);
                return packageFeeds;
            }

            IInstalledAndTransitivePackages installedAndTransitivePackages = await GetInstalledAndTransitivePackagesAsync(projectContextInfos, cancellationToken);

            PackageCollection installedPackageCollection = PackageCollection.FromPackageReferences(installedAndTransitivePackages.InstalledPackages);
            PackageCollection transitivePackageCollection = PackageCollection.FromPackageReferences(installedAndTransitivePackages.TransitivePackages);

            SourceRepository packagesFolderSourceRepository = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
            IEnumerable<SourceRepository> globalPackageFolderRepositories = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);
            var metadataProvider = new MultiSourcePackageMetadataProvider(
                sourceRepositories,
                packagesFolderSourceRepository,
                globalPackageFolderRepositories,
                logger);

            if (itemFilter == ItemFilter.All)
            {
                // if we get here, recommendPackages == true
                packageFeeds.mainFeed = new MultiSourcePackageFeed(sourceRepositories, uiLogger, TelemetryActivity.NuGetTelemetryService);
                packageFeeds.recommenderFeed = new RecommenderPackageFeed(
                    sourceRepositories,
                    installedPackageCollection,
                    transitivePackageCollection,
                    targetFrameworks,
                    metadataProvider,
                    logger);
                return packageFeeds;
            }

            if (itemFilter == ItemFilter.Installed)
            {
                packageFeeds.mainFeed = new InstalledPackageFeed(installedPackageCollection, metadataProvider, logger);
                return packageFeeds;
            }

            if (itemFilter == ItemFilter.Consolidate)
            {
                packageFeeds.mainFeed = new ConsolidatePackageFeed(installedPackageCollection, metadataProvider, logger);
                return packageFeeds;
            }

            // Search all / updates available cannot work without a source repo
            if (sourceRepositories == null)
            {
                return packageFeeds;
            }

            if (itemFilter == ItemFilter.UpdatesAvailable)
            {
                packageFeeds.mainFeed = new UpdatePackageFeed(
                    _serviceBroker,
                    installedPackageCollection,
                    metadataProvider,
                    projectContextInfos.ToArray());

                return packageFeeds;
            }

            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedFeedType, itemFilter));
        }

        private async Task<IReadOnlyList<SourceRepository>> GetGlobalPackageFolderRepositoriesAsync()
        {
            NuGetPackageManager packageManager = await _sharedServiceState.GetPackageManagerAsync(CancellationToken.None);

            return packageManager.GlobalPackageFolderRepositories;
        }

        private async Task<SourceRepository> GetPackagesFolderSourceRepositoryAsync()
        {
            IVsSolutionManager solutionManager = await _sharedServiceState.SolutionManager.GetValueAsync();
            ISettings settings = ServiceLocator.GetInstance<ISettings>();

            return _sharedServiceState.SourceRepositoryProvider.CreateRepository(
                new PackageSource(PackagesFolderPathUtility.GetPackagesFolderPath(solutionManager, settings)),
                FeedType.FileSystemPackagesConfig);
        }
    }
}
