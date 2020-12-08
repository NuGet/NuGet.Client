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

            string cacheId = PackageSearchMetadataCacheObject.GetCacheId(identity.Id, includePrerelease, packageSources);
            if (PackageSearchMetadataMemoryCache.Get(cacheId) is PackageSearchMetadataCacheObject backgroundDataCache)
            {
                PackageSearchMetadataContextInfo packageSearchData = await backgroundDataCache.DetailedPackageSearchMetadataContextInfo;
                PackageDeprecationMetadataContextInfo? deprecatedData = await backgroundDataCache.PackageDeprecationMetadataContextInfo;
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

        public async ValueTask<PackageSearchMetadataContextInfo> GetDetailedPackageSearchMetadataContextInfoAsync(
            PackageIdentity identity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(identity);
            Assumes.NotNullOrEmpty(packageSources);

            string cacheId = PackageSearchMetadataCacheObject.GetCacheId(identity.Id, includePrerelease, packageSources);
            PackageSearchMetadataCacheObject? backgroundDataCache = PackageSearchMetadataMemoryCache.Get(cacheId) as PackageSearchMetadataCacheObject;
            if (backgroundDataCache != null)
            {
                return await backgroundDataCache.DetailedPackageSearchMetadataContextInfo;
            }

            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IPackageSearchMetadata packageMetadata = await packageMetadataProvider.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
            return PackageSearchMetadataContextInfo.Create(packageMetadata);
        }

        public async ValueTask<IReadOnlyCollection<VersionInfoContextInfo>> GetPackageVersionsAsync(
            PackageIdentity identity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(identity);
            Assumes.NotNullOrEmpty(packageSources);

            string cacheId = PackageSearchMetadataCacheObject.GetCacheId(identity.Id, includePrerelease, packageSources);
            PackageSearchMetadataCacheObject? backgroundDataCache = PackageSearchMetadataMemoryCache.Get(cacheId) as PackageSearchMetadataCacheObject;
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

            string cacheId = PackageSearchMetadataCacheObject.GetCacheId(identity.Id, includePrerelease, packageSources);
            PackageSearchMetadataCacheObject? backgroundDataCache = PackageSearchMetadataMemoryCache.Get(cacheId) as PackageSearchMetadataCacheObject;
            if (backgroundDataCache != null)
            {
                return await backgroundDataCache.PackageDeprecationMetadataContextInfo;
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
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            CancellationToken cancellationToken)
        {
            Assumes.NotNullOrEmpty(projectContextInfos);
            Assumes.NotNullOrEmpty(packageSources);
            Assumes.NotNull(searchFilter);

            IReadOnlyCollection<SourceRepository>? sourceRepositories = await _sharedServiceState.GetRepositoriesAsync(packageSources, cancellationToken);
            (IPackageFeed? mainFeed, IPackageFeed? recommenderFeed) = await CreatePackageFeedAsync(projectContextInfos, itemFilter, recommendPackages: false, sourceRepositories, cancellationToken);
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
            IEnumerable<Task<IReadOnlyCollection<IPackageReferenceContextInfo>>> tasks = projectContextInfos
                .Select(project => project.GetInstalledPackagesAsync(_serviceBroker, cancellationToken).AsTask());
            IReadOnlyCollection<IPackageReferenceContextInfo>[] packageReferences = await Task.WhenAll(tasks);
            return packageReferences.SelectMany(e => e).ToList();
        }

        private async Task<(IPackageFeed? mainFeed, IPackageFeed? recommenderFeed)> CreatePackageFeedAsync(
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
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

            IReadOnlyCollection<IPackageReferenceContextInfo> installedPackages = await GetAllInstalledPackagesAsync(projectContextInfos, cancellationToken);

            PackageCollection installedPackageCollection = PackageCollection.FromPackageReferences(installedPackages);

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
                // for now, we are only making recommendations for PC-style projects, and for these the dependent packages are
                // already included in the installedPackages list. When we implement PR-style projects, we'll need to also pass
                // the dependent packages to RecommenderPackageFeed.
                packageFeeds.mainFeed = new MultiSourcePackageFeed(sourceRepositories, uiLogger, TelemetryActivity.NuGetTelemetryService);
                packageFeeds.recommenderFeed = new RecommenderPackageFeed(sourceRepositories.First(), installedPackageCollection, metadataProvider);
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

        private Task<IReadOnlyList<SourceRepository>> GetGlobalPackageFolderRepositoriesAsync()
        {
            var settings = ServiceLocator.GetInstance<ISettings>();
            return Task.FromResult(NuGetPackageManager.GetGlobalPackageFolderRepositories(_sharedServiceState.SourceRepositoryProvider, settings));
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
