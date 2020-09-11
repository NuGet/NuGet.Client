// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
        private ISharedServiceState _sharedServiceState;
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<SourceRepository> _packagesFolderLocalRepositoryLazy;
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<IEnumerable<SourceRepository>> _globalPackageFolderRepositoriesLazy;

        public NuGetPackageSearchService(ServiceActivationOptions options, IServiceBroker sb, AuthorizationServiceClient ac, ISharedServiceState state)
        {
            _options = options;
            _serviceBroker = sb;
            _authorizationServiceClient = ac;
            _sharedServiceState = state;
            _packagesFolderLocalRepositoryLazy = new Microsoft.VisualStudio.Threading.AsyncLazy<SourceRepository>(
                GetPackagesFolderSourceRepositoryAsync,
                NuGetUIThreadHelper.JoinableTaskFactory);
            _globalPackageFolderRepositoriesLazy = new Microsoft.VisualStudio.Threading.AsyncLazy<IEnumerable<SourceRepository>>(
                GetGlobalPackageFolderRepositoriesAsync,
                NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public async ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetAllPackagesAsync(
                IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
                IReadOnlyCollection<PackageSource> packageSources,
                SearchFilter searchFilter,
                ItemFilter itemFilter,
                CancellationToken cancellationToken)
        {
            Assumes.NotNull(projectContextInfos);
            Assumes.NotNull(packageSources);
            Assumes.NotNull(searchFilter);

            bool recommendPackages = false;
            IReadOnlyCollection<SourceRepository> sourceRepositories = await GetRepositoriesAsync(packageSources, cancellationToken);
            (IPackageFeed? mainFeed, IPackageFeed? recommenderFeed) packageFeeds = await CreatePackageFeedAsync(projectContextInfos, itemFilter, recommendPackages, sourceRepositories, cancellationToken);
            Assumes.NotNull(packageFeeds.mainFeed);

            var searchObject = new SearchObject(packageFeeds.mainFeed, packageFeeds.recommenderFeed);
            return await searchObject.GetAllPackagesAsync(searchFilter, cancellationToken);
        }

        public async ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetPackageMetadataListAsync(
            string id,
            IReadOnlyCollection<PackageSource> packageSources,
            bool includePrerelease,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IEnumerable<IPackageSearchMetadata> packageMetadata = await packageMetadataProvider.GetPackageMetadataListAsync(id, includePrerelease, includeUnlisted, cancellationToken);

            return packageMetadata.Select(package => PackageSearchMetadataContextInfo.Create(package)).ToList();
        }


        public async ValueTask<IReadOnlyCollection<VersionInfoContextInfo>> GetPackageVersionsAsync(PackageIdentity identity, IReadOnlyCollection<PackageSource> packageSources, bool includePrerelease, CancellationToken cancellationToken)
        {
            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IPackageSearchMetadata packageMetadata = await packageMetadataProvider.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
            IEnumerable<VersionInfo> versions = await packageMetadata.GetVersionsAsync();
            return await Task.WhenAll(versions.Select(v => VersionInfoContextInfo.CreateAsync(v).AsTask()));
        }

        public async ValueTask<PackageDeprecationMetadataContextInfo?> GetDeprecationMetadataAsync(PackageIdentity identity, IReadOnlyCollection<PackageSource> packageSources, bool includePrerelease, CancellationToken cancellationToken)
        {
            IPackageMetadataProvider packageMetadataProvider = await GetPackageMetadataProviderAsync(packageSources, cancellationToken);
            IPackageSearchMetadata packageMetadata = await packageMetadataProvider.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
            PackageDeprecationMetadata deprecationMetadata = await packageMetadata.GetDeprecationMetadataAsync();
            if(deprecationMetadata == null)
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
                IReadOnlyCollection<PackageSource> packageSources,
                string searchText,
                SearchFilter searchFilter,
                ItemFilter itemFilter,
                bool useRecommender,
                CancellationToken cancellationToken)
        {
            Assumes.NotNull(projectContextInfos);
            Assumes.NotNull(packageSources);
            Assumes.NotNull(searchFilter);

            IReadOnlyCollection<SourceRepository>? sourceRepositories = await GetRepositoriesAsync(packageSources, cancellationToken);
            (IPackageFeed? mainFeed, IPackageFeed? recommenderFeed) = await CreatePackageFeedAsync(projectContextInfos, itemFilter, useRecommender, sourceRepositories, cancellationToken);
            Assumes.NotNull(mainFeed);

            _searchObject = new SearchObject(mainFeed, recommenderFeed);
            return await _searchObject.SearchAsync(searchText, searchFilter, useRecommender, cancellationToken);
        }

        public async ValueTask<int> GetTotalCountAsync(
            int maxCount,
            IReadOnlyCollection<IProjectContextInfo> projectContextInfos,
            IReadOnlyCollection<PackageSource> packageSources,
            SearchFilter searchFilter,
            ItemFilter itemFilter,
            CancellationToken cancellationToken)
        {
            Assumes.NotNull(projectContextInfos);
            Assumes.NotNull(packageSources);
            Assumes.NotNull(searchFilter);

            IReadOnlyCollection<SourceRepository>? sourceRepositories = await GetRepositoriesAsync(packageSources, cancellationToken);
            (IPackageFeed? mainFeed, IPackageFeed? recommenderFeed) = await CreatePackageFeedAsync(projectContextInfos, itemFilter, false, sourceRepositories, cancellationToken);
            Assumes.NotNull(mainFeed);

            var searchObject = new SearchObject(mainFeed, recommenderFeed);
            return await searchObject.GetTotalCountAsync(maxCount, searchFilter, cancellationToken);
        }

        public void Dispose()
        {
            _authorizationServiceClient.Dispose();
        }
    
        private async ValueTask<IPackageMetadataProvider> GetPackageMetadataProviderAsync(IReadOnlyCollection<PackageSource> packageSources, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<SourceRepository> sourceRepositories = await GetRepositoriesAsync(packageSources, cancellationToken);
            SourceRepository localRepo = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
            IEnumerable<SourceRepository> globalRepo = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);
            return new MultiSourcePackageMetadataProvider(sourceRepositories, localRepo, globalRepo, new VisualStudioActivityLogger());
        }

        private async Task<IReadOnlyCollection<SourceRepository>> GetRepositoriesAsync(IReadOnlyCollection<PackageSource> packageSources, CancellationToken cancellationToken)
        {
            ISourceRepositoryProvider? sourceRepositoryProvider = await _sharedServiceState.SourceRepositoryProvider.GetValueAsync(cancellationToken);
            return packageSources.Select(packageSource => sourceRepositoryProvider.CreateRepository(packageSource)).ToList();
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

            using (var projectManagerService = await _serviceBroker.GetProxyAsync<INuGetProjectManagerService>(NuGetServices.ProjectManagerService, _options, cancellationToken))
            {
                Assumes.NotNull(projectManagerService);

                List<string> projectIds = projectContextInfos.Select(pci => pci.ProjectId).ToList();
                IReadOnlyCollection<IPackageReferenceContextInfo> installedPackages = await projectManagerService.GetInstalledPackagesAsync(projectIds, cancellationToken);
                var installedPackageCollection = PackageCollection.FromPackageReferences(installedPackages);

                SourceRepository packagesFolderSourceRepository = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
                IEnumerable<SourceRepository> globalPackageFolderRepositories = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);
                var metadataProvider = new MultiSourcePackageMetadataProvider(sourceRepositories, packagesFolderSourceRepository, globalPackageFolderRepositories, logger);

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
                    packageFeeds.mainFeed = new UpdatePackageFeed(installedPackageCollection, metadataProvider, projectContextInfos.ToArray());
                    return packageFeeds;
                }

                throw new InvalidOperationException("Unsupported feed type");
            }
        }

        private async Task<IEnumerable<SourceRepository>> GetGlobalPackageFolderRepositoriesAsync()
        {
            ISourceRepositoryProvider sourceRepositoryProvider = await _sharedServiceState.SourceRepositoryProvider.GetValueAsync();
            var settings = ServiceLocator.GetInstance<ISettings>();
            return NuGetPackageManager.GetGlobalPackageFolderRepositories(sourceRepositoryProvider, settings);
        }

        private async Task<SourceRepository> GetPackagesFolderSourceRepositoryAsync()
        {
            IVsSolutionManager solutionManager = await _sharedServiceState.SolutionManager.GetValueAsync();
            ISettings settings = ServiceLocator.GetInstance<ISettings>();
            ISourceRepositoryProvider sourceRepositoryProvider = await _sharedServiceState.SourceRepositoryProvider.GetValueAsync();

            return sourceRepositoryProvider.CreateRepository(new PackageSource(PackagesFolderPathUtility.GetPackagesFolderPath(solutionManager, settings)), FeedType.FileSystemPackagesConfig);
        }
    }
}
