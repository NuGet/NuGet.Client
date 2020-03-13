// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a package feed which recommends packages based on currently loaded project info
    /// </summary>
    public class RecommenderPackageFeed : IPackageFeed
    {
        public int PageSize { get; protected set; } = 25;
        public bool IsMultiSource => false;

        private readonly SourceRepository _sourceRepository;
        private readonly IEnumerable<PackageCollectionItem> _installedPackages;
        private readonly IEnumerable<PackageCollectionItem> _dependentPackages;
        private readonly IEnumerable<string> _targetFrameworks;
        private readonly IPackageMetadataProvider _metadataProvider;
        private readonly Common.ILogger _logger;

        public RecommenderPackageFeed(
            SourceRepository sourceRepository,
            IEnumerable<PackageCollectionItem> installedPackages,
            IEnumerable<PackageCollectionItem> dependentPackages,
            IEnumerable<string> targetFrameworks,
            IPackageMetadataProvider metadataProvider,
            Common.ILogger logger)
        {
            if (sourceRepository == null)
            {
                throw new ArgumentNullException(nameof(sourceRepository));
            }
            _sourceRepository = sourceRepository;

            if (installedPackages == null)
            {
                throw new ArgumentNullException(nameof(installedPackages));
            }
            _installedPackages = installedPackages;

            if (dependentPackages == null)
            {
                throw new ArgumentNullException(nameof(dependentPackages));
            }
            _dependentPackages = dependentPackages;

            if (targetFrameworks == null)
            {
                throw new ArgumentNullException(nameof(targetFrameworks));
            }
            _targetFrameworks = targetFrameworks;

            if (metadataProvider == null)
            {
                throw new ArgumentNullException(nameof(metadataProvider));
            }
            _metadataProvider = metadataProvider;

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            _logger = logger;
        }

        private class RecommendSearchToken : ContinuationToken
        {
            public int StartIndex { get; set; }
            public string SearchString { get; set; }
            public SearchFilter SearchFilter { get; set; }
        }

        public Task<SearchResult<IPackageSearchMetadata>> SearchAsync(string searchText, SearchFilter searchFilter, CancellationToken cancellationToken)
        {
            var searchToken = new RecommendSearchToken
            {
                SearchString = searchText,
                SearchFilter = searchFilter,
                StartIndex = 0
            };

            return RecommendPackagesAsync(searchToken, cancellationToken);
        }

        public async Task<SearchResult<IPackageSearchMetadata>> RecommendPackagesAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
        {
            // TODO: Using fixed list of packages for now. Need to get these from the recommender using _installedPackages, _dependentPackages and _targetFrameworks.
            string[] recommendIds = new string[] { "antlr4", "xunit", "moq", "some_bogus_package_name" };
            int _maxRecommend = 5;
            int index = 0;
            List<PackageIdentity> recommendPackages = new List<PackageIdentity>();
            while (recommendIds != null && index < recommendIds.Length && recommendPackages.Count <= _maxRecommend)
            {
                try
                {
                    // TODO: Check that cancellationToken is the correct token to use here.
                    MetadataResource _metadataResource = await _sourceRepository.GetResourceAsync<MetadataResource>(cancellationToken);
                    PackageMetadataResource _packageMetadataResource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

                    Versioning.NuGetVersion ver = await _metadataResource.GetLatestVersion(recommendIds[index], false, false, NullSourceCacheContext.Instance, Common.NullLogger.Instance, cancellationToken);
                    if (ver != null)
                    {
                        NuGet.Packaging.Core.PackageIdentity pid = new NuGet.Packaging.Core.PackageIdentity(recommendIds[index], ver);
                        recommendPackages.Add(pid);
                    }
                }
                catch (System.Exception ex)
                {
                    string ex_string = ex.ToString();
                    // TODO: need to handle exceptions correctly
                }
                index++;
            }
            var packages = recommendPackages.ToArray();

            var searchToken = continuationToken as RecommendSearchToken;
            if (searchToken == null)
            {
                throw new InvalidOperationException("Invalid token");
            }

            var items = await TaskCombinators.ThrottledAsync(
                packages,
                (p, t) => GetPackageMetadataAsync(p, searchToken.SearchFilter.IncludePrerelease, t),
                cancellationToken);

            if(items.Count() < 1)
            {
                return SearchResult.Empty<IPackageSearchMetadata>();
            }
            // The asynchronous execution has randomly returned the packages, so we need to resort
            // based on the original recommendation order.
            var result = SearchResult.FromItems(items.OrderBy(p => Array.IndexOf(packages, p.Identity)).ToArray());

            // Set status to indicate that there are no more items to load
            result.SourceSearchStatus = new Dictionary<string, LoadingStatus>
            {
                { _sourceRepository.ToString().ToLower(), LoadingStatus.NoMoreItems }
            };

            result.NextToken = null;

            return result;
        }

        public Task<SearchResult<IPackageSearchMetadata>> ContinueSearchAsync(ContinuationToken continuationToken, CancellationToken cancellationToken)
            => Task.FromResult(SearchResult.Empty<IPackageSearchMetadata>());

        public Task<SearchResult<IPackageSearchMetadata>> RefreshSearchAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
            => Task.FromResult(SearchResult.Empty<IPackageSearchMetadata>());

        public async Task<IPackageSearchMetadata> GetPackageMetadataAsync(PackageIdentity identity, bool includePrerelease, CancellationToken cancellationToken)
        {
            // first we try and load the metadata from a local package
            var packageMetadata = await _metadataProvider.GetLocalPackageMetadataAsync(identity, includePrerelease, cancellationToken);
            if (packageMetadata == null)
            {
                // and failing that we go to the network
                packageMetadata = await _metadataProvider.GetPackageMetadataAsync(identity, includePrerelease, cancellationToken);
            }
            return packageMetadata;
        }

    }
}
