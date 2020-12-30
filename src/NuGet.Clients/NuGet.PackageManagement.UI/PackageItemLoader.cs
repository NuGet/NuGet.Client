// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;
using ContractItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI
{
    internal class PackageItemLoader : IPackageItemLoader, IDisposable
    {
        private readonly PackageLoadContext _context;
        private readonly string _searchText;
        private readonly bool _includePrerelease;
        private readonly IReadOnlyCollection<PackageSourceContextInfo> _packageSources;
        private readonly ContractItemFilter _itemFilter;
        private readonly bool _useRecommender;
        private PackageCollection _installedPackages;
        private IEnumerable<IPackageReferenceContextInfo> _packageReferences;
        private PackageFeedSearchState _state = new PackageFeedSearchState();
        private SearchFilter _searchFilter;
        private INuGetSearchService _searchService;
        public IItemLoaderState State => _state;
        private IServiceBroker _serviceBroker;
        public bool IsMultiSource => _packageSources.Count > 1;

        private PackageItemLoader(
            IServiceBroker serviceBroker,
            PackageLoadContext context,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            ContractItemFilter itemFilter,
            string searchText,
            bool includePrerelease,
            bool useRecommender)
        {
            Assumes.NotNull(serviceBroker);
            Assumes.NotNull(context);
            Assumes.NotNullOrEmpty(packageSources);

            _serviceBroker = serviceBroker;
            _context = context;
            _searchText = searchText ?? string.Empty;
            _includePrerelease = includePrerelease;
            _packageSources = packageSources;
            _itemFilter = itemFilter;
            _useRecommender = useRecommender;
        }

        public static async ValueTask<PackageItemLoader> CreateAsync(
            IServiceBroker serviceBroker,
            PackageLoadContext context,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            ContractItemFilter itemFilter,
            string searchText = null,
            bool includePrerelease = true,
            bool useRecommender = true)
        {
            var itemLoader = new PackageItemLoader(
                serviceBroker,
                context,
                packageSources,
                itemFilter,
                searchText,
                includePrerelease,
                useRecommender);

            await itemLoader.InitializeAsync();

            return itemLoader;
        }

        // For unit testing purposes
        internal static async ValueTask<PackageItemLoader> CreateAsync(
            IServiceBroker serviceBroker,
            PackageLoadContext context,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            ContractItemFilter itemFilter,
            INuGetSearchService searchService,
            string searchText = null,
            bool includePrerelease = true,
            bool useRecommender = true)
        {
            var itemLoader = new PackageItemLoader(
                serviceBroker,
                context,
                packageSources,
                itemFilter,
                searchText,
                includePrerelease,
                useRecommender);

            await itemLoader.InitializeAsync(searchService);

            return itemLoader;
        }

        private async ValueTask InitializeAsync(INuGetSearchService searchService = null)
        {
            _searchFilter = new SearchFilter(includePrerelease: _includePrerelease)
            {
                SupportedFrameworks = await _context.GetSupportedFrameworksAsync()
            };

            _searchService = searchService ?? await GetSearchServiceAsync(CancellationToken.None);
            _serviceBroker.AvailabilityChanged += OnAvailabilityChanged;
        }

        private void OnAvailabilityChanged(object sender, BrokeredServicesChangedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                _searchService?.Dispose();
                _searchService = await GetSearchServiceAsync(CancellationToken.None);
            }).PostOnFailure(nameof(PackageItemLoader), nameof(OnAvailabilityChanged));
        }

        private async ValueTask<INuGetSearchService> GetSearchServiceAsync(CancellationToken cancellationToken)
        {
#pragma warning disable ISB001 // Dispose of proxies
            INuGetSearchService searchService = await _serviceBroker.GetProxyAsync<INuGetSearchService>(NuGetServices.SearchService, cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies
            Assumes.NotNull(searchService);
            return searchService;
        }

        public async Task<int> GetTotalCountAsync(int maxCount, CancellationToken cancellationToken)
        {
            // Go off the UI thread to perform non-UI operations
            await TaskScheduler.Default;
            IReadOnlyCollection<string> targetFrameworks = await _context.GetSupportedFrameworksAsync();

            return await _searchService.GetTotalCountAsync(maxCount, _context.Projects, _packageSources, targetFrameworks, _searchFilter, _itemFilter, cancellationToken);
        }

        public async Task<IReadOnlyCollection<PackageSearchMetadataContextInfo>> GetInstalledAndTransitivePackagesAsync(CancellationToken cancellationToken)
        {
            // Go off the UI thread to perform non-UI operations
            await TaskScheduler.Default;

            ActivityCorrelationId.StartNew();
            IReadOnlyCollection<string> targetFrameworks = await _context.GetSupportedFrameworksAsync();

            return await _searchService.GetAllPackagesAsync(_context.Projects, _packageSources, targetFrameworks, _searchFilter, _itemFilter, cancellationToken);
        }

        public async Task LoadNextAsync(IProgress<IItemLoaderState> progress, CancellationToken cancellationToken)
        {
            ActivityCorrelationId.StartNew();

            cancellationToken.ThrowIfCancellationRequested();

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadBegin);

            await UpdateStateAndReportAsync(
                new SearchResultContextInfo(Array.Empty<PackageSearchMetadataContextInfo>(),
                    ImmutableDictionary<string, LoadingStatus>.Empty,
                    hasMoreItems: _state.Results?.HasMoreItems ?? false),
                progress,
                cancellationToken);

            SearchResultContextInfo searchResult = await SearchAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await UpdateStateAndReportAsync(searchResult, progress, cancellationToken);

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadEnd);
        }

        public async Task UpdateStateAsync(IProgress<IItemLoaderState> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadBegin);

            progress?.Report(_state);

            SearchResultContextInfo searchResult = await _searchService.RefreshSearchAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateStateAndReportAsync(searchResult, progress, cancellationToken);

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadEnd);
        }

        public async Task<SearchResultContextInfo> SearchAsync(CancellationToken cancellationToken)
        {
            await TaskScheduler.Default;
            cancellationToken.ThrowIfCancellationRequested();

            if (_state.Results != null && _state.Results.HasMoreItems)
            {
                // only continue search for the search package feed, not the recommender.
                return await _searchService.ContinueSearchAsync(cancellationToken);
            }
            IReadOnlyCollection<string> targetFrameworks = await _context.GetSupportedFrameworksAsync();

            return await _searchService.SearchAsync(_context.Projects, _packageSources, targetFrameworks, _searchText, _searchFilter, _itemFilter, _useRecommender, cancellationToken);
        }

        public async Task UpdateStateAndReportAsync(SearchResultContextInfo searchResult, IProgress<IItemLoaderState> progress, CancellationToken cancellationToken)
        {
            // cache installed packages here for future use
            _installedPackages = await _context.GetInstalledPackagesAsync();

            // fetch package references from all the projects and cache locally
            // for solution view, we'll always show the highest available version
            // but for project view, get the allowed version range and pass it to package item view model to choose the latest version based on that
            if (_packageReferences == null && !_context.IsSolution)
            {
                IEnumerable<Task<IReadOnlyCollection<IPackageReferenceContextInfo>>> tasks = _context.Projects
                    .Select(project => project.GetInstalledPackagesAsync(
                        _context.ServiceBroker,
                        cancellationToken).AsTask());
                _packageReferences = (await Task.WhenAll(tasks)).SelectMany(p => p).Where(p => p != null);
            }

            var state = new PackageFeedSearchState(searchResult);
            _state = state;
            progress?.Report(state);
        }

        public void Reset()
        {
            _state = new PackageFeedSearchState();
        }

        public IEnumerable<PackageItemListViewModel> GetCurrent()
        {
            if (_state.ItemsCount == 0)
            {
                return Enumerable.Empty<PackageItemListViewModel>();
            }

            var listItemViewModels = new List<PackageItemListViewModel>();

            foreach (PackageSearchMetadataContextInfo metadata in _state.Results.PackageSearchItems)
            {
                VersionRange allowedVersions = VersionRange.All;

                // get the allowed version range and pass it to package item view model to choose the latest version based on that
                if (_packageReferences != null)
                {
                    IEnumerable<IPackageReferenceContextInfo> matchedPackageReferences = _packageReferences.Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.Identity.Id, metadata.Identity.Id));
                    VersionRange[] allowedVersionsRange = matchedPackageReferences.Select(r => r.AllowedVersions).Where(v => v != null).ToArray();

                    if (allowedVersionsRange.Length > 0)
                    {
                        allowedVersions = allowedVersionsRange[0];
                    }
                }

                var listItem = new PackageItemListViewModel
                {
                    Id = metadata.Identity.Id,
                    Version = metadata.Identity.Version,
                    IconUrl = metadata.IconUrl,
                    Author = metadata.Authors,
                    DownloadCount = metadata.DownloadCount,
                    Summary = metadata.Summary,
                    AllowedVersions = allowedVersions,
                    PrefixReserved = metadata.PrefixReserved && !IsMultiSource,
                    Versions = AsyncLazy.New(() => { return GetVersionInfoAsync(metadata.Identity); }),
                    DeprecationMetadata = AsyncLazy.New(() => { return GetDeprecationMetadataAsync(metadata.Identity); }),
                    DetailedPackageSearchMetadata = AsyncLazy.New(() => { return GetDetailedPackageSearchMetadataContextInfoAsync(metadata.Identity); }),
                    Recommended = metadata.IsRecommended,
                    RecommenderVersion = metadata.RecommenderVersion,
                    Vulnerabilities = metadata.Vulnerabilities,
                    Sources = _packageSources,
                    // PackageReader = metadata.PackageReader, //(metadata as PackageSearchMetadataBuilder.ClonedPackageSearchMetadata)?.PackageReader, SCOBAN
                };

                listItem.UpdatePackageStatus(_installedPackages);

                if (!_context.IsSolution && _context.PackageManagerProviders.Any())
                {
                    listItem.ProvidersLoader = AsyncLazy.New(
                        async () =>
                        {
                            string uniqueProjectName = await _context.Projects[0].GetUniqueNameOrNameAsync(
                                _context.ServiceBroker,
                                CancellationToken.None);

                            return await AlternativePackageManagerProviders.CalculateAlternativePackageManagersAsync(
                                _context.PackageManagerProviders,
                                listItem.Id,
                                uniqueProjectName);
                        });
                }
                listItemViewModels.Add(listItem);
            }

            return listItemViewModels.ToArray();
        }

        private async Task<PackageDeprecationMetadataContextInfo> GetDeprecationMetadataAsync(PackageIdentity identity)
        {
            Assumes.NotNull(identity);

            return await _searchService.GetDeprecationMetadataAsync(identity, _packageSources, _includePrerelease, CancellationToken.None);
        }

        private async Task<IReadOnlyCollection<VersionInfoContextInfo>> GetVersionInfoAsync(PackageIdentity identity)
        {
            Assumes.NotNull(identity);

            return await _searchService.GetPackageVersionsAsync(identity, _packageSources, _includePrerelease, CancellationToken.None);
        }

        private async Task<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo)> GetDetailedPackageSearchMetadataContextInfoAsync(PackageIdentity identity)
        {
            Assumes.NotNull(identity);

            return await _searchService.GetPackageMetadataAsync(identity, _packageSources, _includePrerelease, CancellationToken.None);
        }

        public void Dispose()
        {
            _searchService?.Dispose();

            if (_serviceBroker != null)
            {
                _serviceBroker.AvailabilityChanged -= OnAvailabilityChanged;
            }
        }

        private class PackageFeedSearchState : IItemLoaderState
        {
            private readonly SearchResultContextInfo _results;

            public PackageFeedSearchState()
            {
            }

            public PackageFeedSearchState(SearchResultContextInfo results)
            {
                _results = results ?? throw new ArgumentNullException(nameof(results));
            }

            public SearchResultContextInfo Results => _results;

            public Guid? OperationId => _results?.OperationId;

            public LoadingStatus LoadingStatus
            {
                get
                {
                    if (_results == null || SourceLoadingStatus == null || SourceLoadingStatus.Values == null)
                    {
                        // initial status when no load called before
                        return LoadingStatus.Unknown;
                    }

                    return SourceLoadingStatus.Values.Aggregate();
                }
            }

            // returns the "raw" counter which is not the same as _results.Items.Count
            // simply because it correlates to un-merged items
            public int ItemsCount => _results?.PackageSearchItems.Count ?? 0;

            public IReadOnlyDictionary<string, LoadingStatus> SourceLoadingStatus => _results?.SourceLoadingStatus;
        }
    }
}
