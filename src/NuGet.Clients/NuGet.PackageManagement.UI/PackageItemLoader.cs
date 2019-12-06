// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    internal class PackageItemLoader : IPackageItemLoader
    {
        private readonly PackageLoadContext _context;
        private readonly string _searchText;
        private readonly bool _includePrerelease;

        private readonly IPackageFeed _packageFeed;
        private PackageCollection _installedPackages;
        private IEnumerable<Packaging.PackageReference> _packageReferences;

        private SearchFilter SearchFilter => new SearchFilter(includePrerelease: _includePrerelease)
        {
            SupportedFrameworks = _context.GetSupportedFrameworks()
        };

        // Never null
        private PackageFeedSearchState _state = new PackageFeedSearchState();

        public IItemLoaderState State => _state;

        public bool IsMultiSource => _packageFeed.IsMultiSource;

        private class PackageFeedSearchState : IItemLoaderState
        {
            private readonly SearchResult<IPackageSearchMetadata> _results;

            public PackageFeedSearchState()
            {
            }

            public PackageFeedSearchState(SearchResult<IPackageSearchMetadata> results)
            {
                if (results == null)
                {
                    throw new ArgumentNullException(nameof(results));
                }
                _results = results;
            }

            public SearchResult<IPackageSearchMetadata> Results => _results;

            public Guid? OperationId => _results?.OperationId;

            public LoadingStatus LoadingStatus
            {
                get
                {
                    if (_results == null)
                    {
                        // initial status when no load called before
                        return LoadingStatus.Unknown;
                    }

                    return (SourceLoadingStatus?.Values).Aggregate();
                }
            }

            // returns the "raw" counter which is not the same as _results.Items.Count
            // simply because it correlates to un-merged items
            public int ItemsCount => _results?.RawItemsCount ?? 0;

            public IDictionary<string, LoadingStatus> SourceLoadingStatus => _results?.SourceSearchStatus;
        }

        public PackageItemLoader(
            PackageLoadContext context,
            IPackageFeed packageFeed,
            string searchText = null,
            bool includePrerelease = true)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            _context = context;

            if (packageFeed == null)
            {
                throw new ArgumentNullException(nameof(packageFeed));
            }
            _packageFeed = packageFeed;

            _searchText = searchText ?? string.Empty;
            _includePrerelease = includePrerelease;
        }

        public async Task<int> GetTotalCountAsync(int maxCount, CancellationToken cancellationToken)
        {
            // Go off the UI thread to perform non-UI operations
            await TaskScheduler.Default;

            ActivityCorrelationId.StartNew();

            int totalCount = 0;
            ContinuationToken nextToken = null;
            do
            {
                var searchResult = await SearchAsync(nextToken, cancellationToken);
                while (searchResult.RefreshToken != null)
                {
                    searchResult = await _packageFeed.RefreshSearchAsync(searchResult.RefreshToken, cancellationToken);
                }
                totalCount += searchResult.Items?.Count() ?? 0;
                nextToken = searchResult.NextToken;
            } while (nextToken != null && totalCount <= maxCount);

            return totalCount;
        }

        public async Task<IReadOnlyList<IPackageSearchMetadata>> GetAllPackagesAsync(CancellationToken cancellationToken)
        {
            // Go off the UI thread to perform non-UI operations
            await TaskScheduler.Default;

            ActivityCorrelationId.StartNew();

            var packages = new List<IPackageSearchMetadata>();
            ContinuationToken nextToken = null;
            do
            {
                var searchResult = await SearchAsync(nextToken, cancellationToken);
                while (searchResult.RefreshToken != null)
                {
                    searchResult = await _packageFeed.RefreshSearchAsync(searchResult.RefreshToken, cancellationToken);
                }

                nextToken = searchResult.NextToken;

                packages.AddRange(searchResult.Items);

            } while (nextToken != null);

            return packages;
        }

        public async Task LoadNextAsync(IProgress<IItemLoaderState> progress, CancellationToken cancellationToken)
        {
            ActivityCorrelationId.StartNew();

            cancellationToken.ThrowIfCancellationRequested();

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadBegin);

            var nextToken = _state.Results?.NextToken;
            var cleanState = SearchResult.Empty<IPackageSearchMetadata>();
            cleanState.NextToken = nextToken;
            await UpdateStateAndReportAsync(cleanState, progress, cancellationToken);

            var searchResult = await SearchAsync(nextToken, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await UpdateStateAndReportAsync(searchResult, progress, cancellationToken);

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadEnd);
        }

        public async Task UpdateStateAsync(IProgress<IItemLoaderState> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadBegin);

            progress?.Report(_state);

            var refreshToken = _state.Results?.RefreshToken;
            if (refreshToken != null)
            {
                var searchResult = await _packageFeed.RefreshSearchAsync(refreshToken, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                await UpdateStateAndReportAsync(searchResult, progress, cancellationToken);
            }

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadEnd);
        }

        public async Task<SearchResult<IPackageSearchMetadata>> SearchAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
        {
            await TaskScheduler.Default;
            // check if there is already a running initialization task for SolutionManager. If yes,
            // search should wait until this is completed. This would usually happen when opening manager
            //ui is the first nuget operation under LSL mode where it might take some time to initialize.
            if (_context.SolutionManager.InitializationTask != null && !_context.SolutionManager.InitializationTask.IsCompleted)
            {
                await _context.SolutionManager.InitializationTask;
            }

            if (continuationToken != null)
            {
                return await _packageFeed.ContinueSearchAsync(continuationToken, cancellationToken);
            }

            return await _packageFeed.SearchAsync(_searchText, SearchFilter, cancellationToken);
        }

        public async Task UpdateStateAndReportAsync(SearchResult<IPackageSearchMetadata> searchResult, IProgress<IItemLoaderState> progress, CancellationToken cancellationToken)
        {
            // cache installed packages here for future use
            _installedPackages = await _context.GetInstalledPackagesAsync();

            // fetch package references from all the projects and cache locally
            // for solution view, we'll always show the highest available version
            // but for project view, get the allowed version range and pass it to package item view model to choose the latest version based on that
            if (_packageReferences == null && !_context.IsSolution)
            {
                var tasks = _context.Projects
                    .Select(project => project.GetInstalledPackagesAsync(cancellationToken));
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

            var listItems = _state.Results
                .Select(metadata =>
                {
                    VersionRange allowedVersions = VersionRange.All;

                    // get the allowed version range and pass it to package item view model to choose the latest version based on that
                    if (_packageReferences != null)
                    {
                        var matchedPackageReferences = _packageReferences.Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.PackageIdentity.Id, metadata.Identity.Id));
                        var allowedVersionsRange = matchedPackageReferences.Select(r => r.AllowedVersions).Where(v => v != null).ToArray();

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
                        Versions = AsyncLazy.New(metadata.GetVersionsAsync),
                        AllowedVersions = allowedVersions,
                        PrefixReserved = metadata.PrefixReserved && !IsMultiSource,
                        DeprecationMetadata = AsyncLazy.New(metadata.GetDeprecationMetadataAsync),
                        LocalPackageInfo = (metadata as PackageSearchMetadataBuilder.ClonedPackageSearchMetadata)?.LocalPackageInfo
                    };

                    listItem.UpdatePackageStatus(_installedPackages);

                    if (!_context.IsSolution && _context.PackageManagerProviders.Any())
                    {
                        listItem.ProvidersLoader = AsyncLazy.New(
                            () => AlternativePackageManagerProviders.CalculateAlternativePackageManagersAsync(
                                _context.PackageManagerProviders,
                                listItem.Id,
                                _context.Projects[0]));
                    }

                    return listItem;
                });

            return listItems.ToArray();
        }
    }
}
