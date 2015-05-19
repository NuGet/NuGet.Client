// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLoader : ILoader
    {
        private readonly SourceRepository _sourceRepository;

        private readonly NuGetProject[] _projects;

        // The list of all installed packages. This variable is used for the package status calculation.
        private readonly HashSet<PackageIdentity> _installedPackages;

        private readonly HashSet<string> _installedPackageIds;

        private readonly NuGetPackageManager _packageManager;

        private readonly PackageLoaderOption _option;

        private readonly string _searchText;

        // The list of packages that have updates available
        private List<UISearchMetadata> _packagesWithUpdates;

        public PackageLoader(PackageLoaderOption option,
            NuGetPackageManager packageManager,
            IEnumerable<NuGetProject> projects,
            SourceRepository sourceRepository,
            string searchText)
        {
            _sourceRepository = sourceRepository;
            _packageManager = packageManager;
            _projects = projects.ToArray();
            _option = option;
            _searchText = searchText;

            LoadingMessage = string.IsNullOrWhiteSpace(searchText) ?
                Resources.Text_Loading :
                string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_Searching,
                    searchText);

            _installedPackages = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
            _installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public string LoadingMessage { get; }

        private async Task<SearchResult> SearchAsync(int startIndex, CancellationToken ct)
        {
            if (_option.Filter == Filter.Installed)
            {
                // show only the installed packages
                return await SearchInstalledAsync(startIndex, ct);
            }

            // Search all / updates available cannot work without a source repo
            if (_sourceRepository == null)
            {
                return SearchResult.Empty;
            }

            if (_option.Filter == Filter.UpdatesAvailable)
            {
                return await SearchUpdatesAsync(startIndex, ct);
            }

            // normal search
            var searchResource = await _sourceRepository.GetResourceAsync<UISearchResource>();

            // search in source
            if (searchResource == null)
            {
                return SearchResult.Empty;
            }
            else
            {
                var searchFilter = new SearchFilter();
                searchFilter.IncludePrerelease = _option.IncludePrerelease;
                searchFilter.SupportedFrameworks = GetSupportedFrameworks();

                var searchResults = await searchResource.Search(
                    _searchText,
                    searchFilter,
                    startIndex,
                    _option.PageSize + 1,
                    ct);

                var items = searchResults.ToList();

                var hasMoreItems = items.Count > _option.PageSize;

                if (hasMoreItems)
                {
                    items.RemoveAt(items.Count - 1);
                }

                return new SearchResult
                {
                    Items = items,
                    HasMoreItems = hasMoreItems,
                };
            }
        }

        // Returns the list of frameworks that we need to pass to the server during search
        private IEnumerable<string> GetSupportedFrameworks()
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in _projects)
            {
                NuGetFramework framework;
                if (project.TryGetMetadata(NuGetProjectMetadataKeys.TargetFramework,
                    out framework))
                {
                    if (framework != null
                        && framework.IsAny)
                    {
                        // One of the project's target framework is AnyFramework. In this case,
                        // we don't need to pass the framework filter to the server.
                        return Enumerable.Empty<string>();
                    }

                    if (framework != null
                        && framework.IsSpecificFramework)
                    {
                        frameworks.Add(framework.DotNetFrameworkName);
                    }
                }
                else
                {
                    // we also need to process SupportedFrameworks
                    IEnumerable<NuGetFramework> supportedFrameworks;
                    if (project.TryGetMetadata(
                        NuGetProjectMetadataKeys.SupportedFrameworks,
                        out supportedFrameworks))
                    {
                        foreach (var f in supportedFrameworks)
                        {
                            if (f.IsAny)
                            {
                                return Enumerable.Empty<string>();
                            }

                            frameworks.Add(f.DotNetFrameworkName);
                        }
                    }
                }
            }

            return frameworks;
        }

        /// <summary>
        /// Returns the grouped list of installed packages.
        /// </summary>
        /// <param name="latest">
        /// If true, the latest version is returned. Otherwise, the oldest
        /// version is returned.
        /// </param>
        /// <returns></returns>
        private async Task<IEnumerable<PackageIdentity>> GetInstalledPackagesAsync(bool latest, CancellationToken token)
        {
            var installedPackages = new Dictionary<string, PackageIdentity>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var project in _projects)
            {
                foreach (var package in (await project.GetInstalledPackagesAsync(token)))
                {
                    if (!(project is INuGetIntegratedProject)
                        &&
                        !_packageManager.PackageExistsInPackagesFolder(package.PackageIdentity))
                    {
                        continue;
                    }

                    PackageIdentity p;
                    if (installedPackages.TryGetValue(package.PackageIdentity.Id, out p))
                    {
                        if (latest)
                        {
                            if (p.Version < package.PackageIdentity.Version)
                            {
                                installedPackages[package.PackageIdentity.Id] = package.PackageIdentity;
                            }
                        }
                        else
                        {
                            if (p.Version > package.PackageIdentity.Version)
                            {
                                installedPackages[package.PackageIdentity.Id] = package.PackageIdentity;
                            }
                        }
                    }
                    else
                    {
                        installedPackages[package.PackageIdentity.Id] = package.PackageIdentity;
                    }
                }
            }

            return installedPackages.Values;
        }

        private async Task<SearchResult> SearchInstalledAsync(int startIndex, CancellationToken cancellationToken)
        {
            var installedPackages = (await GetInstalledPackagesAsync(latest: true, token: cancellationToken))
                .Where(p => p.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id)
                .Skip(startIndex)
                .Take(_option.PageSize + 1)
                .ToArray();

            var results = new List<UISearchMetadata>();
            var localResource = await _packageManager.PackagesFolderSourceRepository
                .GetResourceAsync<UIMetadataResource>();

            var metadataResource =
                _sourceRepository == null ?
                null :
                await _sourceRepository.GetResourceAsync<UIMetadataResource>();
            var tasks = new List<Task<UISearchMetadata>>();
            for (int i = 0; i < installedPackages.Length; i++)
            {
                var packageIdentity = installedPackages[i];

                tasks.Add(
                    Task.Run(() =>
                        GetPackageMetadataAsync(localResource,
                                                metadataResource,
                                                packageIdentity,
                                                cancellationToken)));
            }

            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                results.Add(task.Result);
            }

            return new SearchResult
            {
                Items = results,
                HasMoreItems = installedPackages.Length > _option.PageSize,
            };
        }

        // Gets the package metadata from the local resource when the remote source 
        // is not available.
        private static async Task<UISearchMetadata> GetPackageMetadataWhenRemoteSourceUnavailable(
            UIMetadataResource localResource,
            PackageIdentity identity,
            CancellationToken cancellationToken)
        {
            UIPackageMetadata packageMetadata = null;
            if (localResource != null)
            {
                var localMetadata = await localResource.GetMetadata(
                    identity.Id,
                    includePrerelease: true,
                    includeUnlisted: true,
                    token: cancellationToken);
                packageMetadata = localMetadata.FirstOrDefault(p => p.Identity.Version == identity.Version);
            }

            string summary = string.Empty;
            string title = identity.Id;
            if (packageMetadata != null)
            {
                summary = packageMetadata.Summary;
                if (string.IsNullOrEmpty(summary))
                {
                    summary = packageMetadata.Description;
                }
                if (!string.IsNullOrEmpty(packageMetadata.Title))
                {
                    title = packageMetadata.Title;
                }
            }

            var versions = new List<VersionInfo>
            {
                new VersionInfo(identity.Version, downloadCount: null)
            };

            return new UISearchMetadata(
                identity,
                title: title,
                summary: summary,
                iconUrl: packageMetadata == null ? null : packageMetadata.IconUrl,
                versions: ToLazyTask(versions),
                latestPackageMetadata: null);
        }

        private async Task<UISearchMetadata> GetPackageMetadataFromMetadataResourceAsync(
            UIMetadataResource metadataResource,
            PackageIdentity identity,
            CancellationToken cancellationToken)
        {
            var uiPackageMetadatas = await metadataResource.GetMetadata(
                identity.Id,
                _option.IncludePrerelease,
                includeUnlisted: false,
                token: cancellationToken);
            var packageMetadata = uiPackageMetadatas.FirstOrDefault(p => p.Identity.Version == identity.Version);

            string summary = string.Empty;
            string title = identity.Id;
            if (packageMetadata != null)
            {
                summary = packageMetadata.Summary;
                if (string.IsNullOrEmpty(summary))
                {
                    summary = packageMetadata.Description;
                }
                if (!string.IsNullOrEmpty(packageMetadata.Title))
                {
                    title = packageMetadata.Title;
                }
            }

            var versions = uiPackageMetadatas.OrderByDescending(m => m.Identity.Version)
                .Select(m => new VersionInfo(m.Identity.Version, m.DownloadCount));
            return new UISearchMetadata(
                identity,
                title: title,
                summary: summary,
                iconUrl: packageMetadata == null ? null : packageMetadata.IconUrl,
                versions: ToLazyTask(versions),
                latestPackageMetadata: packageMetadata);
        }

        private async Task<UISearchMetadata> GetPackageMetadataAsync(
            UIMetadataResource localResource,
            UIMetadataResource metadataResource,
            PackageIdentity identity,
            CancellationToken cancellationToken)
        {
            if (metadataResource == null)
            {
                return await GetPackageMetadataWhenRemoteSourceUnavailable(
                    localResource,
                    identity,
                    cancellationToken);
            }

            try
            {
                return await GetPackageMetadataFromMetadataResourceAsync(
                    metadataResource,
                    identity,
                    cancellationToken);
            }
            catch
            {
                // The remote source is not available. In this case, if the user
                // is searching for installed packages, NuGet should not fail but
                // should use the local resource instead.
                if (localResource != null)
                {
                    return await GetPackageMetadataWhenRemoteSourceUnavailable(
                        localResource,
                        identity,
                        cancellationToken);
                }
                else
                {
                    throw;
                }
            }
        }

        // Search in installed packages that have updates available
        private async Task<SearchResult> SearchUpdatesAsync(int startIndex, CancellationToken ct)
        {
            if (_packagesWithUpdates == null)
            {
                await CreatePackagesWithUpdatesAsync(ct);
            }

            var items = _packagesWithUpdates.Skip(startIndex).Take(_option.PageSize + 1).ToList();

            var hasMoreItems = items.Count > _option.PageSize + startIndex;

            if (hasMoreItems)
            {
                items.RemoveAt(items.Count - 1);
            }

            return new SearchResult
            {
                Items = items,
                HasMoreItems = hasMoreItems,
            };
        }

        // Creates the list of installed packages that have updates available
        private async Task CreatePackagesWithUpdatesAsync(CancellationToken ct)
        {
            _packagesWithUpdates = new List<UISearchMetadata>();
            var metadataResource = await _sourceRepository.GetResourceAsync<UIMetadataResource>();

            if (metadataResource == null)
            {
                return;
            }

            var installedPackages = (await GetInstalledPackagesAsync(latest: false, token: ct))
                .Where(p => p.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id);

            foreach (var package in installedPackages)
            {
                // only release packages respect the prerel option
                var includePre = _option.IncludePrerelease || package.Version.IsPrerelease;

                var data = await metadataResource.GetMetadata(package.Id, includePre, false, ct);
                var highest = data.OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault();

                if (highest != null)
                {
                    if (VersionComparer.VersionRelease.Compare(package.Version, highest.Identity.Version) < 0)
                    {
                        var allVersions = data
                            .OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease)
                            .Select(e => new VersionInfo(e.Identity.Version, e.DownloadCount));

                        var lazyVersions = ToLazyTask(allVersions);

                        var summary = string.IsNullOrEmpty(highest.Summary) ? highest.Description : highest.Summary;

                        var title = string.IsNullOrEmpty(highest.Title) ? highest.Identity.Id : highest.Title;

                        _packagesWithUpdates.Add(new UISearchMetadata(highest.Identity, title, summary, highest.IconUrl, lazyVersions, highest));
                    }
                }
            }
        }

        public async Task<LoadResult> LoadItemsAsync(int startIndex, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadBegin);

            List<SearchResultPackageMetadata> packages = new List<SearchResultPackageMetadata>();

            var results = await SearchAsync(startIndex, cancellationToken);

            int resultCount = 0;

            foreach (var package in results.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                resultCount++;

                var searchResultPackage = new SearchResultPackageMetadata(_sourceRepository);
                searchResultPackage.Id = package.Identity.Id;
                searchResultPackage.Version = package.Identity.Version;
                searchResultPackage.IconUrl = package.IconUrl;

                var versionList = new Lazy<Task<IEnumerable<VersionInfo>>>(async () =>
                {
                    var versions = await package.Versions.Value;

                    var filteredVersions = versions
                            .Where(v => !v.Version.IsPrerelease || _option.IncludePrerelease)
                            .ToList();

                    if (!filteredVersions.Any(v => v.Version == searchResultPackage.Version))
                    {
                        filteredVersions.Add(new VersionInfo(searchResultPackage.Version, downloadCount: null));
                    }

                    return filteredVersions;
                });

                searchResultPackage.Versions = versionList;

                searchResultPackage.StatusProvider = new Lazy<Task<PackageStatus>>(
                    () => CalculatePackageStatus(searchResultPackage.Id, versionList));

                // filter out prerelease version when needed.
                if (searchResultPackage.Version.IsPrerelease &&
                    !_option.IncludePrerelease)
                {
                    var status = await searchResultPackage.StatusProvider.Value;

                    if (status == PackageStatus.NotInstalled)
                    {
                        continue;
                    }
                }

                if (_option.Filter == Filter.UpdatesAvailable)
                {
                    var status = await searchResultPackage.StatusProvider.Value;

                    if (status != PackageStatus.UpdateAvailable)
                    {
                        continue;
                    }
                }

                searchResultPackage.Summary = package.Summary;
                packages.Add(searchResultPackage);
            }

            cancellationToken.ThrowIfCancellationRequested();
            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageLoadEnd);
            return new LoadResult()
            {
                Items = packages,
                HasMoreItems = results.HasMoreItems,
                NextStartIndex = startIndex + resultCount
            };
        }

        // Returns the package status for the searchPackageResult
        private async Task<PackageStatus> CalculatePackageStatus(string id, Lazy<Task<IEnumerable<VersionInfo>>> versions)
        {
            if (_installedPackageIds.Contains(id))
            {
                var versionsUnwrapped = await versions.Value;

                var highestAvailableVersion = versionsUnwrapped
                                                .Select(v => v.Version)
                                                .Max();

                var highestInstalled = _installedPackages
                    .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, id))
                    .OrderByDescending(p => p.Version, VersionComparer.Default)
                    .First();

                if (VersionComparer.VersionRelease.Compare(highestInstalled.Version, highestAvailableVersion) < 0)
                {
                    return PackageStatus.UpdateAvailable;
                }

                return PackageStatus.Installed;
            }

            return PackageStatus.NotInstalled;
        }

        public async Task InitializeAsync()
        {
            // create _installedPackages and _installedPackageIds
            foreach (var project in _projects)
            {
                var installedPackagesInProject = await project.GetInstalledPackagesAsync(CancellationToken.None);
                if (!(project is INuGetIntegratedProject))
                {
                    installedPackagesInProject = installedPackagesInProject.Where(
                        p =>
                            _packageManager.PackageExistsInPackagesFolder(p.PackageIdentity));
                }

                foreach (var package in installedPackagesInProject)
                {
                    _installedPackages.Add(package.PackageIdentity);
                    _installedPackageIds.Add(package.PackageIdentity.Id);
                }
            }
        }

        private static Lazy<Task<IEnumerable<VersionInfo>>> ToLazyTask(IEnumerable<VersionInfo> versions)
        {
            return new Lazy<Task<IEnumerable<VersionInfo>>>(() => Task.FromResult(versions));
        }
    }
}