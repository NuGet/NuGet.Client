// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat.ListPackage;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal sealed class SponsorReportProcessor
    {
        private readonly ListPackageArgs _listPackageArgs;
        private readonly Dictionary<string, PackageSponsorshipResult> _sponsorshipCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<PackageSource, SourceRepository> _sourceRepositoryCache = new();
        private readonly Dictionary<PackageSource, PackageMetadataResource> _packageMetadataResourceCache = new();
        private bool _sourcesPrepared;

        internal SponsorReportProcessor(ListPackageArgs listPackageArgs)
        {
            _listPackageArgs = listPackageArgs;
        }

        internal bool Configure()
        {
            PackageSourceMapping packageSourceMapping = _listPackageArgs.PackageSourceMapping;
            bool hasExplicitSources = _listPackageArgs.ExplicitPackageSources.Count > 0;

            if (hasExplicitSources && packageSourceMapping.IsEnabled)
            {
                _listPackageArgs.Renderer.AddProblem(
                    ProblemType.Error,
                    Strings.ListPkg_SponsorPackageSourceMappingWithSource);
                return false;
            }

            if (hasExplicitSources)
            {
                _listPackageArgs.UseExplicitPackageSources();
            }

            if (_listPackageArgs.Renderer is ListPackageConsoleRenderer)
            {
                _listPackageArgs.Logger.LogMinimal(Strings.ListPkg_SponsorCheckingSourcesAndProjects);

                if (packageSourceMapping.IsEnabled)
                {
                    _listPackageArgs.Logger.LogInformation(Strings.ListPkg_SponsorPackageSourceMappingEnabled);
                }
            }

            if (_listPackageArgs.Renderer is ListPackageConsoleRenderer consoleRenderer)
            {
                consoleRenderer.ShowSponsorshipSourceHint =
                    !hasExplicitSources &&
                    !packageSourceMapping.IsEnabled &&
                    !_listPackageArgs.PackageSources.Any(source => UriUtility.IsNuGetOrg(source.Source));
            }

            PopulateSourceRepositoryCache();
            return true;
        }

        internal async Task<bool> PrepareSourcesAsync(ListPackageReportModel reportModel)
        {
            var unsupportedSources = new HashSet<PackageSource>();

            foreach (PackageSource source in _listPackageArgs.PackageSources)
            {
                PackageMetadataResource? resource =
                    await _sourceRepositoryCache[source].GetResourceAsync<PackageMetadataResource>(
                        _listPackageArgs.CancellationToken);

                if (resource?.SupportsPackageIdMetadata == true)
                {
                    _packageMetadataResourceCache[source] = resource;
                }
                else
                {
                    unsupportedSources.Add(source);
                }
            }

            reportModel.SponsorshipUnsupportedSources = _listPackageArgs.PackageSources
                .Where(unsupportedSources.Contains)
                .ToList();
            _sourcesPrepared = true;

            return _packageMetadataResourceCache.Count > 0;
        }

        internal async Task<bool> ProcessProjectAsync(
            List<FrameworkPackages> frameworks,
            ListPackageProjectModel projectModel)
        {
            List<string> packageIds = ListPackageCommandRunner.GetPackageIds(frameworks, includeTransitive: true);
            projectModel.HasPackages = packageIds.Count > 0;

            Dictionary<string, List<PackageSource>> sourcesById = packageIds.ToDictionary(
                id => id,
                FilterSourcesByPackageSourceMapping,
                StringComparer.OrdinalIgnoreCase);

            var selectedSources = sourcesById.Values.SelectMany(sources => sources).ToHashSet();
            List<PackageSource> packageSources = _listPackageArgs.PackageSources.Where(selectedSources.Contains).ToList();
            List<PackageSource> httpSources = HttpSourcesUtility.GetDisallowedInsecureHttpSources(packageSources);
            if (httpSources.Count > 0)
            {
                projectModel.AddProjectInformation(
                    ProblemType.Error,
                    HttpSourcesUtility.BuildHttpSourceErrorMessage(httpSources, "list package"));
                return false;
            }

            Dictionary<string, List<PackageSponsorship>> sponsorships =
                await GetSponsorshipMetadataAsync(sourcesById, projectModel);
            UpdatePackagesWithSponsorshipMetadata(frameworks, sponsorships);
            return true;
        }

        internal List<PackageSource> FilterSourcesByPackageSourceMapping(string package)
        {
            PackageSourceMapping sourceMapping = _listPackageArgs.PackageSourceMapping;

            if (!sourceMapping.IsEnabled)
            {
                return _sourcesPrepared
                    ? _listPackageArgs.PackageSources
                        .Where(_packageMetadataResourceCache.ContainsKey)
                        .ToList()
                    : _listPackageArgs.PackageSources;
            }

            IReadOnlyList<string> mappedSourceNames = sourceMapping.GetConfiguredPackageSources(package);

            return _listPackageArgs.PackageSources
                .Where(source =>
                    (!_sourcesPrepared || _packageMetadataResourceCache.ContainsKey(source)) &&
                    mappedSourceNames.Contains(source.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        internal List<PackageSponsorship> OrderSponsorshipsByConfiguredSource(
            IEnumerable<PackageSponsorship> sponsorships)
        {
            List<PackageSource> configuredSources = _listPackageArgs.PackageSources;

            return sponsorships
                .OrderBy(sponsorship =>
                {
                    int index = configuredSources.FindIndex(
                        source => string.Equals(
                            source.Source,
                            sponsorship.Source,
                            StringComparison.Ordinal));
                    return index < 0 ? int.MaxValue : index;
                })
                .ToList();
        }

        private async Task<Dictionary<string, List<PackageSponsorship>>> GetSponsorshipMetadataAsync(
            Dictionary<string, List<PackageSource>> sourcesById,
            ListPackageProjectModel projectModel)
        {
            List<string> packageIds = sourcesById.Keys.ToList();
            var sponsorshipsById = new Dictionary<string, List<PackageSponsorship>>(
                capacity: packageIds.Count,
                comparer: StringComparer.OrdinalIgnoreCase);
            var queriedSourceSet = new HashSet<PackageSource>();

            await ListPackageCommandRunner.ThrottledForEachAsync(
                packageIds,
                (packageId, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return _sponsorshipCache.TryGetValue(packageId, out PackageSponsorshipResult? cachedResult)
                        ? Task.FromResult(cachedResult)
                        : GetSponsorshipsForPackageAsync(
                            packageId,
                            sourcesById[packageId],
                            cancellationToken);
                },
                result =>
                {
                    _sponsorshipCache[result.PackageId] = result;
                    sponsorshipsById[result.PackageId] = result.Sponsorships;
                    queriedSourceSet.UnionWith(result.QueriedSources);
                },
                ListPackageCommandRunner.GetMaxParallel(_listPackageArgs),
                _listPackageArgs.CancellationToken);

            projectModel.SponsorshipQueriedSources = _listPackageArgs.PackageSources
                .Where(queriedSourceSet.Contains)
                .ToList();

            return sponsorshipsById;
        }

        private async Task<PackageSponsorshipResult> GetSponsorshipsForPackageAsync(
            string package,
            List<PackageSource> sources,
            CancellationToken cancellationToken)
        {
            var sponsorships = new List<PackageSponsorship>();
            var queriedSources = new List<PackageSource>();

            await ListPackageCommandRunner.ThrottledForEachAsync(
                sources,
                (source, innerCancellationToken) =>
                    GetSponsorshipFromSourceAsync(source, package, innerCancellationToken),
                result =>
                {
                    queriedSources.Add(result.Source);

                    if (result.Sponsorship != null)
                    {
                        sponsorships.Add(result.Sponsorship);
                    }
                },
                sources.Count,
                cancellationToken);

            return new PackageSponsorshipResult(
                package,
                OrderSponsorshipsByConfiguredSource(sponsorships),
                queriedSources);
        }

        private async Task<(
            PackageSource Source,
            PackageSponsorship? Sponsorship)> GetSponsorshipFromSourceAsync(
                PackageSource packageSource,
                string package,
                CancellationToken cancellationToken)
        {
            PackageMetadataResource packageMetadataResource = _packageMetadataResourceCache[packageSource];

            using var sourceCacheContext = new SourceCacheContext();
            PackageIdMetadata? metadata = await packageMetadataResource.GetPackageIdMetadataAsync(
                package,
                sourceCacheContext,
                _listPackageArgs.Logger,
                cancellationToken);

            PackageSponsorship? sponsorship = metadata == null || metadata.SponsorshipUrls.Count == 0
                ? null
                : new PackageSponsorship(packageSource.Source, metadata.SponsorshipUrls);

            return (packageSource, sponsorship);
        }

        private void PopulateSourceRepositoryCache()
        {
            IEnumerable<Lazy<INuGetResourceProvider>> providers = Repository.Provider.GetCoreV3();
            foreach (PackageSource source in _listPackageArgs.PackageSources)
            {
                _sourceRepositoryCache[source] = Repository.CreateSource(providers, source, FeedType.Undefined);
            }
        }

        private static void UpdatePackagesWithSponsorshipMetadata(
            List<FrameworkPackages> frameworks,
            Dictionary<string, List<PackageSponsorship>> sponsorshipsById)
        {
            foreach (InstalledPackageReference package in frameworks.SelectMany(
                framework => framework.TopLevelPackages.Concat(framework.TransitivePackages)))
            {
                if (sponsorshipsById.TryGetValue(package.Name, out List<PackageSponsorship>? sponsorships))
                {
                    package.Sponsorships = sponsorships;
                }
            }
        }

        private sealed class PackageSponsorshipResult
        {
            internal PackageSponsorshipResult(
                string packageId,
                List<PackageSponsorship> sponsorships,
                IReadOnlyList<PackageSource> queriedSources)
            {
                PackageId = packageId;
                Sponsorships = sponsorships;
                QueriedSources = queriedSources;
            }

            internal string PackageId { get; }
            internal List<PackageSponsorship> Sponsorships { get; }
            internal IReadOnlyList<PackageSource> QueriedSources { get; }
        }
    }
}
