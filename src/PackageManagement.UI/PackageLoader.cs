using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using NuGet.Client.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Client;
using NuGet.Frameworks;
using System.Runtime.Versioning;
using NuGet.PackagingCore;
using NuGet.Packaging;

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

        private HashSet<PackageIdentity> _installedPackages;
        private HashSet<string> _installedPackageIds;
        private bool _installedPackagesLoaded = false;

        private PackageLoaderOption _option;

        private string _searchText;

        private const int _pageSize = 10;

        // Copied from file Constants.cs in NuGet.Core:
        // This is temporary until we fix the gallery to have proper first class support for this.
        // The magic unpublished date is 1900-01-01T00:00:00
        public static readonly DateTimeOffset Unpublished = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8));

        public PackageLoader(PackageLoaderOption option, IEnumerable<NuGetProject> projects, SourceRepository sourceRepository, string searchText)
        {
            _sourceRepository = sourceRepository;
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

            // show only the installed packages
            if (_option.Filter == Filter.Installed)
            {
                // TODO: get metadata from packages folder instead
                Dictionary<PackageIdentity, List<string>> byId = new Dictionary<PackageIdentity, List<string>>(); 

                foreach (var project in _projects)
                {
                    string name = string.Empty;
                    project.TryGetMetadata<string>(NuGetProjectMetadataKeys.Name, out name);

                    foreach (var package in project.GetInstalledPackages())
                    {
                        List<string> list = null;

                        if (!byId.TryGetValue(package.PackageIdentity, out list))
                        {
                            list = new List<string>();
                            byId.Add(package.PackageIdentity, list);
                        }

                        list.Add(name);
                    }
                }

                // create metadata
                foreach (var id in byId.Keys.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
                {
                    string summary = String.Join(", ", byId[id].Where(e => !String.IsNullOrEmpty(e)).OrderBy(e => e, StringComparer.OrdinalIgnoreCase));

                    results.Add(new UISearchMetadata(id, summary, null, new NuGetVersion[] { id.Version }, null));
                }
            }
            // installed packages with updates
            else if (_option.Filter == Filter.UpdatesAvailable)
            {
                var metadataResource = await _sourceRepository.GetResourceAsync<UIMetadataResource>();

                if (metadataResource == null)
                {
                    return Enumerable.Empty<UISearchMetadata>();
                }

                var installedPackages = _projects.SelectMany(e => e.GetInstalledPackages())
                    .Select(e => e.PackageIdentity).Distinct(PackageIdentity.Comparer)
                    .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase);

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
                            var allVersions = data.Select(e => e.Identity.Version).OrderByDescending(e => e, VersionComparer.VersionRelease);

                            string summary = String.IsNullOrEmpty(highest.Summary) ? highest.Description : highest.Summary;

                            results.Add(new UISearchMetadata(highest.Identity, summary, highest.IconUrl, allVersions, highest));
                        }
                    }
                }
            }
            // normal search
            else
            {
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
                    var frameworks = new List<string>();

                    // TODO: re-enable this once the V3 server supports full framework names
                    //foreach (var project in _projects)
                    //{
                    //    NuGetFramework framework = project.GetMetadata<NuGetFramework>("TargetFramework");

                    //    if (framework != null && framework.IsSpecificFramework)
                    //    {
                    //        frameworks.Add(framework.DotNetFrameworkName);
                    //    }
                    //}

                    searchFilter.SupportedFrameworks = frameworks;

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

        public async Task<LoadResult> LoadItems(int startIndex, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Load up the packages from the project for the package status
            // TODO: move this somewhere else? refresh it?
            if (!_installedPackagesLoaded)
            {
                _installedPackagesLoaded = true;

                foreach (var package in _projects.SelectMany(p => p.GetInstalledPackages()))
                {
                    _installedPackages.Add(package.PackageIdentity);
                    _installedPackageIds.Add(package.PackageIdentity.Id);
                }
            }

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

                //if (searchResultPackage.IconUrl == null)
                //{
                //    // use the default
                //    searchResultPackage.IconUrl = new Uri("https://nuget.org/Content/Images/packageDefaultIcon.png");
                //}

                // get other versions
                var versionList = package.Versions.ToList();
                if (!_option.IncludePrerelease)
                {
                    // remove prerelease version if includePrelease is false
                    versionList.RemoveAll(v => v.IsPrerelease);
                }

                if (!versionList.Contains(searchResultPackage.Version))
                {
                    versionList.Add(searchResultPackage.Version);
                }

                searchResultPackage.Versions = versionList;
                searchResultPackage.Status = GetStatus(new PackageIdentity(searchResultPackage.Id, searchResultPackage.Version));

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
                HasMoreItems = resultCount == _pageSize,
                NextStartIndex = startIndex + resultCount
            };
        }

        private PackageStatus GetStatus(PackageIdentity package)
        {
            if (_installedPackageIds.Contains(package.Id))
            {
                // check for an exact match
                if (_installedPackages.Contains(package))
                {
                    return PackageStatus.Installed;
                }

                // find our highest version to compare
                var highestInstalled = _installedPackages.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, package.Id)).OrderByDescending(p => p.Version, VersionComparer.Default).First();

                if (VersionComparer.VersionRelease.Compare(highestInstalled.Version, package.Version) < 0)
                {
                    return PackageStatus.UpdateAvailable;
                }

                return PackageStatus.Installed;
            }

            return PackageStatus.NotInstalled;
        }
    }
}