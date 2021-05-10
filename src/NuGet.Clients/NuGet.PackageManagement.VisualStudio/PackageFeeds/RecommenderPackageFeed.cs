// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DataAI.NuGetRecommender.Contracts;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a package feed which recommends packages based on currently loaded project info
    /// </summary>
    public class RecommenderPackageFeed : IPackageFeed
    {
        public bool IsMultiSource => false;

        private readonly SourceRepository _sourceRepository;
        private readonly List<string> _installedPackages;
        private readonly List<string> _transitivePackages;
        private readonly IReadOnlyCollection<string> _targetFrameworks;
        private readonly IPackageMetadataProvider _metadataProvider;
        private readonly Common.ILogger _logger;

        public (string modelVersion, string vsixVersion) VersionInfo { get; set; } = (modelVersion: (string)null, vsixVersion: (string)null);

        private readonly AsyncLazy<IVsNuGetPackageRecommender> _nuGetRecommender;

        private IVsNuGetPackageRecommender NuGetRecommender { get; set; }

        private const int MaxRecommended = 5;

        public RecommenderPackageFeed(
            IEnumerable<SourceRepository> sourceRepositories,
            PackageCollection installedPackages,
            PackageCollection transitivePackages,
            IReadOnlyCollection<string> targetFrameworks,
            IPackageMetadataProvider metadataProvider,
            Common.ILogger logger)
        {
            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }
            if (installedPackages is null)
            {
                throw new ArgumentNullException(nameof(installedPackages));
            }
            if (transitivePackages is null)
            {
                throw new ArgumentNullException(nameof(transitivePackages));
            }
            _targetFrameworks = targetFrameworks ?? throw new ArgumentNullException(nameof(targetFrameworks));
            _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // The recommender package feed should only created when one of the sources is nuget.org.
            if (sourceRepositories.Any(item => TelemetryUtility.IsNuGetOrg(item.PackageSource.Source)))
            {
                _sourceRepository = sourceRepositories.First(item => TelemetryUtility.IsNuGetOrg(item.PackageSource.Source));

                _installedPackages = installedPackages.Select(item => item.Id).ToList();
                _transitivePackages = transitivePackages.Select(item => item.Id).ToList();

                _nuGetRecommender = new AsyncLazy<IVsNuGetPackageRecommender>(
                    async () =>
                    {
                        return await AsyncServiceProvider.GlobalProvider.GetServiceAsync<SVsNuGetRecommenderService, IVsNuGetPackageRecommender>();
                    },
                    NuGetUIThreadHelper.JoinableTaskFactory);
            }
        }

        private class RecommendSearchToken : ContinuationToken
        {
            public string SearchString { get; set; }
            public SearchFilter SearchFilter { get; set; }
        }

        public Task<SearchResult<IPackageSearchMetadata>> SearchAsync(string searchText, SearchFilter searchFilter, CancellationToken cancellationToken)
        {
            var searchToken = new RecommendSearchToken
            {
                SearchString = searchText,
                SearchFilter = searchFilter,
            };

            return RecommendPackagesAsync(searchToken, cancellationToken);
        }

        public async Task<SearchResult<IPackageSearchMetadata>> RecommendPackagesAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
        {
            var searchToken = continuationToken as RecommendSearchToken;
            if (searchToken is null)
            {
                throw new ArgumentException("Invalid continuation token", nameof(continuationToken));
            }
            // don't make recommendations if the user entered a search string
            if (!string.IsNullOrEmpty(searchToken.SearchString))
            {
                return SearchResult.Empty<IPackageSearchMetadata>();
            }

            // get recommender service and version info
            if (NuGetRecommender is null && _nuGetRecommender != null)
            {
                try
                {
                    NuGetRecommender = await _nuGetRecommender.GetValueAsync(cancellationToken);
                }
                catch (ServiceUnavailableException)
                {
                    // if the recommender service is not available, NuGetRecommender remains null and we show only the default package list
                }
                if (!(NuGetRecommender is null))
                {
                    var VersionDict = NuGetRecommender.GetVersionInfo();
                    VersionInfo = (modelVersion: VersionDict.ContainsKey("Model") ? VersionDict["Model"] : (string)null,
                                    vsixVersion: VersionDict.ContainsKey("Vsix") ? VersionDict["Vsix"] : (string)null);
                }
            }

            List<string> recommendIds = null;
            if (!(NuGetRecommender is null))
            {
                // call the recommender to get package recommendations
                recommendIds = await NuGetRecommender.GetRecommendedPackageIdsAsync(_targetFrameworks, _installedPackages, _transitivePackages, cancellationToken);
                recommendIds = recommendIds.Take(MaxRecommended).ToList();
            }

            if (recommendIds is null || !recommendIds.Any())
            {
                return SearchResult.Empty<IPackageSearchMetadata>();
            }

            // get metadata by doing a search for each recommended package id
            var searchFilter = new SearchFilter(includePrerelease: false);
            IEnumerable<Task<SearchResult<IPackageSearchMetadata>>> searchTasks = recommendIds.Select(p => _sourceRepository.SearchAsync(string.Format("packageid:{0}", p), searchFilter, pageSize:1, cancellationToken));
            SearchResult<IPackageSearchMetadata>[] searchItems = await System.Threading.Tasks.Task.WhenAll(searchTasks);
            IEnumerable<IPackageSearchMetadata> resultItems = searchItems.Where(i => i.RawItemsCount > 0)?.Select(si => si.Items[0]);
            if (!resultItems.Any())
            {
                return SearchResult.Empty<IPackageSearchMetadata>();
            }
            SearchResult<IPackageSearchMetadata> result = SearchResult.FromItems(resultItems?.OrderBy(p => recommendIds.IndexOf(p.Identity.Id)).ToArray());

            // Set status to indicate that there are no more items to load
            result.SourceSearchStatus = new Dictionary<string, LoadingStatus>
            {
                { _sourceRepository.PackageSource.Name, LoadingStatus.NoMoreItems }
            };
            result.NextToken = null;

            return result;
        }

        public Task<SearchResult<IPackageSearchMetadata>> ContinueSearchAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult(SearchResult.Empty<IPackageSearchMetadata>());

        public Task<SearchResult<IPackageSearchMetadata>> RefreshSearchAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult(SearchResult.Empty<IPackageSearchMetadata>());
    }
}
