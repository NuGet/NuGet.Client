using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using NuGet.Frameworks;
using NuGet.Protocol.VisualStudio;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    internal class PackageLoaderOption
    {
        public PackageLoaderOption(
            Filter filter,
            bool includePrerelease)
        {
            Filter = filter;
            IncludePrerelease = includePrerelease;
        }

        public Filter Filter { get; private set; }

        public bool IncludePrerelease { get; private set; }
    }

    internal class PackageLoader : ILoader
    {
        private SourceRepository _sourceRepository;

        private NuGetProject[] _projects;

        // The list of all installed packages. This variable is used for the package status calculation.
        private HashSet<PackageIdentity> _installedPackages;
        private HashSet<string> _installedPackageIds;

        private NuGetPackageManager _packageManager;

        private PackageLoaderOption _option;

        private string _searchText;

        private const int _pageSize = 10;

        // Copied from file Constants.cs in NuGet.Core:
        // This is temporary until we fix the gallery to have proper first class support for this.
        // The magic unpublished date is 1900-01-01T00:00:00
        public static readonly DateTimeOffset Unpublished = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8));

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

        public string LoadingMessage
        {
            get;
            private set;
        }

        private async Task<IEnumerable<UISearchMetadata>> Search(int startIndex, CancellationToken ct)
        {
            List<UISearchMetadata> results = new List<UISearchMetadata>();

            if (_sourceRepository == null)
            {
                return results;
            }

            if (_option.Filter == Filter.Installed)
            {
                // show only the installed packages
                return await SearchInstalled(startIndex, ct);
            }
            else if (_option.Filter == Filter.UpdatesAvailable)
            {
                return await SearchUpdates(startIndex, ct);
            }
            else
            {
                // normal search
                var searchResource = await _sourceRepository.GetResourceAsync<UISearchResource>();

                // search in source
                if (searchResource == null)
                {
                    return Enumerable.Empty<UISearchMetadata>();
                }
                else
                {
                    var searchFilter = new SearchFilter();
                    searchFilter.IncludePrerelease = _option.IncludePrerelease;
                    searchFilter.SupportedFrameworks = GetSupportedFrameworks();

                    results.AddRange(await searchResource.Search(
                        _searchText,
                        searchFilter,
                        startIndex,
                        _pageSize,
                        ct));
                }
            }

            return results;
        }

        // Returns the list of frameworks that we need to pass to the server during search
        IEnumerable<string> GetSupportedFrameworks()
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in _projects)
            {
                NuGetFramework framework;
                if (project.TryGetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework,
                    out framework))
                {
                    if (framework != null && framework.IsAny)
                    {
                        // One of the project's target framework is AnyFramework. In this case, 
                        // we don't need to pass the framework filter to the server.
                        return Enumerable.Empty<string>();
                    }

                    if (framework != null && framework.IsSpecificFramework)
                    {
                        frameworks.Add(framework.DotNetFrameworkName);
                    }
                }
                else
                {
                    // we also need to process SupportedFrameworks
                    IEnumerable<NuGetFramework> supportedFrameworks;
                    if (project.TryGetMetadata<IEnumerable<NuGetFramework>>(
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
        /// <param name="latest">If true, the latest version is returned. Otherwise, the oldest
        /// version is returned.</param>
        /// <returns></returns>
        private async Task<IEnumerable<PackageIdentity>> GetInstalledPackages(bool latest, CancellationToken token)
        {
            Dictionary<string, PackageIdentity> installedPackages = new Dictionary<string, PackageIdentity>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var project in _projects)
            {
                foreach (var package in (await project.GetInstalledPackagesAsync(token)))
                {
                    if (!(project is NuGet.ProjectManagement.Projects.ProjectKNuGetProjectBase) &&
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

        private async Task<IEnumerable<UISearchMetadata>> SearchInstalled(int startIndex, CancellationToken ct)
        {
            var installedPackages = (await GetInstalledPackages(latest: true, token: ct))
                .Where(p => p.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id)
                .Skip(startIndex)
                .Take(_pageSize);

            List<UISearchMetadata> results = new List<UISearchMetadata>();
            var localResource = await _packageManager.PackagesFolderSourceRepository
                .GetResourceAsync<UIMetadataResource>();

            var metadataResource = await _sourceRepository.GetResourceAsync<UIMetadataResource>();
            var tasks = new List<Task<UISearchMetadata>>();
            foreach (var identity in installedPackages)
            {
                var task = Task.Run(
                    async () =>
                    {
                        UIPackageMetadata packageMetadata = null;
                        if (localResource != null)
                        {
                            // try get metadata from local resource
                            var localMetadata = await localResource.GetMetadata(identity.Id,
                                includePrerelease: true,
                                includeUnlisted: true,
                                token: ct);
                            packageMetadata = localMetadata.FirstOrDefault(p => p.Identity.Version == identity.Version);
                        }

                        var metadata = await metadataResource.GetMetadata(
                            identity.Id,
                            _option.IncludePrerelease,
                            false,
                            ct);
                        var versions = metadata.Select(m => m.Identity.Version)
                            .OrderByDescending(v => v);
                        if (packageMetadata == null)
                        {
                            // package metadata can't be found in local resource. Try find it in remote resource.
                            packageMetadata = metadata.FirstOrDefault(p => p.Identity.Version == identity.Version);
                        }

                        string summary = string.Empty;
                        string title = identity.Id;
                        if (packageMetadata != null)
                        {
                            summary = packageMetadata.Summary;
                            if (String.IsNullOrEmpty(summary))
                            {
                                summary = packageMetadata.Description;
                            }
                            if (!string.IsNullOrEmpty(packageMetadata.Title))
                            {
                                title = packageMetadata.Title;
                            }
                        }

                        return new UISearchMetadata(
                            identity,
                            title: title,
                            summary: summary,
                            iconUrl: packageMetadata == null ? null : packageMetadata.IconUrl,
                            versions: versions.Select(v => new VersionInfo(v, 0)),
                            latestPackageMetadata: packageMetadata);
                    });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());
            foreach (var task in tasks)
            {
                results.Add(task.Result);
            }

            return results;
        }

        // Search in installed packages that have updates available
        private async Task<IEnumerable<UISearchMetadata>> SearchUpdates(int startIndex, CancellationToken ct)
        {
            if (_packagesWithUpdates == null)
            {
                await CreatePackagesWithUpdates(ct);
            }

            return _packagesWithUpdates.Skip(startIndex).Take(_pageSize);
        }

        // Creates the list of installed packages that have updates available
        private async Task CreatePackagesWithUpdates(CancellationToken ct)
        {
            _packagesWithUpdates = new List<UISearchMetadata>();
            var metadataResource = await _sourceRepository.GetResourceAsync<UIMetadataResource>();
            if (metadataResource == null)
            {
                return;
            }

            var installedPackages = (await GetInstalledPackages(latest: false, token: ct))
                .Where(p => p.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(p => p.Id);
            foreach (var package in installedPackages)
            {
                // only release packages respect the prerel option
                bool includePre = _option.IncludePrerelease || package.Version.IsPrerelease;

                var data = await metadataResource.GetMetadata(package.Id, includePre, false, ct);
                var highest = data.OrderByDescending(e => e.Identity.Version, VersionComparer.VersionRelease).FirstOrDefault();

                if (highest != null)
                {
                    if (VersionComparer.VersionRelease.Compare(package.Version, highest.Identity.Version) < 0)
                    {
                        var allVersions = data.Select(e => e.Identity.Version)
                            .OrderByDescending(e => e, VersionComparer.VersionRelease)
                            .Select(v => new VersionInfo(v, 0));

                        string summary = String.IsNullOrEmpty(highest.Summary) ? highest.Description : highest.Summary;
                        string title = string.IsNullOrEmpty(highest.Title) ? highest.Identity.Id : highest.Title;

                        _packagesWithUpdates.Add(new UISearchMetadata(highest.Identity, title, summary, highest.IconUrl, allVersions, highest));
                    }
                }
            }
        }

        public async Task<LoadResult> LoadItems(int startIndex, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();            

            List<SearchResultPackageMetadata> packages = new List<SearchResultPackageMetadata>();
            var results = await Search(startIndex, ct);
            int resultCount = 0;
            foreach (var package in results)
            {
                ct.ThrowIfCancellationRequested();
                ++resultCount;

                var searchResultPackage = new SearchResultPackageMetadata(_sourceRepository);
                searchResultPackage.Id = package.Identity.Id;
                searchResultPackage.Version = package.Identity.Version;
                searchResultPackage.IconUrl = package.IconUrl;

                // get other versions
                var versionList = package.Versions.ToList();
                if (!_option.IncludePrerelease)
                {
                    // remove prerelease version if includePrelease is false
                    versionList.RemoveAll(v => v.Version.IsPrerelease);
                }

                if (!versionList.Select(v => v.Version).Contains(searchResultPackage.Version))
                {
                    versionList.Add(new VersionInfo(searchResultPackage.Version, 0));
                }

                searchResultPackage.Versions = versionList;
                searchResultPackage.Status = CalculatePackageStatus(searchResultPackage);

                // filter out prerelease version when needed.
                if (searchResultPackage.Version.IsPrerelease &&
                   !_option.IncludePrerelease &&
                    searchResultPackage.Status == PackageStatus.NotInstalled)
                {
                    continue;
                }

                if (_option.Filter == Filter.UpdatesAvailable &&
                    searchResultPackage.Status != PackageStatus.UpdateAvailable)
                {
                    continue;
                }

                searchResultPackage.Summary = package.Summary;
                packages.Add(searchResultPackage);
            }

            ct.ThrowIfCancellationRequested();
            return new LoadResult()
            {
                Items = packages,
                HasMoreItems = resultCount != 0,
                NextStartIndex = startIndex + resultCount
            };
        }

        // Returns the package status for the searchPackageResult
        private PackageStatus CalculatePackageStatus(SearchResultPackageMetadata searchPackageResult)
        {
            if (_installedPackageIds.Contains(searchPackageResult.Id))
            {
                var highestAvailableVersion = searchPackageResult.Versions
                    .Select(v => v.Version)
                    .Max();
                                
                var highestInstalled = _installedPackages
                    .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, searchPackageResult.Id))
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
        
        public async Task Initialize()
        {
            // create _installedPackages and _installedPackageIds
            foreach (var project in _projects)
            {
                var installedPackagesInProject = await project.GetInstalledPackagesAsync(CancellationToken.None);
                if (!(project is ProjectManagement.Projects.ProjectKNuGetProjectBase))
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
    }
}