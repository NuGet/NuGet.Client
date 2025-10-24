// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Repositories;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Commands.Package.PackageDownload
{
    internal static class PackageDownloadRunner
    {
        internal const int ExitCodeError = 1;
        internal const int ExitCodeSuccess = 0;

        public static async Task<int> RunAsync(PackageDownloadArgs args, CancellationToken token)
        {
            ILoggerWithColor logger = new CommandOutputLogger(args.LogLevel)
            {
                HidePrefixForInfoAndMinimal = true
            };

            XPlatUtility.ConfigureProtocol();
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(logger, !args.Interactive);
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                args.ConfigFile,
                new XPlatMachineWideSetting());
            IReadOnlyList<PackageSource> packageSources = GetPackageSources(args.Sources, new PackageSourceProvider(settings));

            return await RunAsync(args, logger, packageSources, settings, token);
        }

        public static async Task<int> RunAsync(PackageDownloadArgs args, ILoggerWithColor logger, IReadOnlyList<PackageSource> packageSources, ISettings settings, CancellationToken token)
        {
            var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
            var hasSourcesArg = args.Sources != null && args.Sources.Count > 0;
            var mappingDisabled = (packageSourceMapping != null && !packageSourceMapping.IsEnabled) || packageSourceMapping == null;
            if ((mappingDisabled || hasSourcesArg) && DetectAndReportInsecureSources(args.AllowInsecureConnections, packageSources, logger))
            {
                return ExitCodeError;
            }

            string outputDirectory = args.OutputDirectory ?? Directory.GetCurrentDirectory();
            var cache = new SourceCacheContext();

            IReadOnlyDictionary<string, SourceRepository> sourceRepositoriesMap = GetSourceRepositories(packageSources);

            bool downloadedAllSuccessfully = true;

            foreach (var package in args.Packages ?? [])
            {
                logger.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PackageDownloadCommand_Starting,
                    package.Id,
                    string.IsNullOrEmpty(package.NuGetVersion?.ToNormalizedString()) ? Strings.PackageDownloadCommand_LatestVersion : package.NuGetVersion.ToNormalizedString()));

                // Resolve which repositories to use for this package
                if (!TryGetRepositoriesForPackage(
                        package.Id,
                        args,
                        packageSources,
                        packageSourceMapping,
                        sourceRepositoriesMap,
                        logger,
                        out List<SourceRepository> sourceRepositories))
                {
                    return ExitCodeError;
                }

                try
                {
                    (NuGetVersion? version, SourceRepository? downloadRepository) =
                        await ResolvePackageDownloadVersion(
                            package,
                            sourceRepositories,
                            cache,
                            logger,
                            args.IncludePrerelease,
                            token);

                    if (version == null)
                    {
                        // Unable to find a valid version
                        downloadedAllSuccessfully &= false;
                        continue;
                    }

                    bool success = await DownloadPackageAsync(
                        package.Id,
                        version,
                        downloadRepository!,
                        cache,
                        settings,
                        outputDirectory,
                        logger,
                        token);

                    if (success)
                    {
                        logger.LogMinimal(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.PackageDownloadCommand_Succeeded,
                            package.Id,
                            version,
                            outputDirectory));
                    }
                    else
                    {
                        logger.LogError(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.PackageDownloadCommand_Failed,
                            package.Id,
                            version));

                        downloadedAllSuccessfully &= false;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                    downloadedAllSuccessfully &= false;
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            return downloadedAllSuccessfully ? ExitCodeSuccess : ExitCodeError;
        }

        internal static async Task<(NuGetVersion?, SourceRepository?)> ResolvePackageDownloadVersion(
            PackageWithNuGetVersion packageWithNuGetVersion,
            IEnumerable<SourceRepository> sourceRepositories,
            SourceCacheContext cache,
            ILoggerWithColor logger,
            bool includePrerelease,
            CancellationToken token)
        {
            NuGetVersion? versionToDownload = null;
            SourceRepository? downloadSourceRepository = null;
            bool versionSpecified = packageWithNuGetVersion.NuGetVersion != null;

            foreach (var repo in sourceRepositories)
            {
                var finder = await repo.GetResourceAsync<PackageMetadataResource>(token);
                var packages = await finder.GetMetadataAsync(
                    packageWithNuGetVersion.Id,
                    includePrerelease,
                    includeUnlisted: versionSpecified, // only load unlisted if an exact version is specified
                    sourceCacheContext: cache,
                    logger,
                    token);

                if (packages == null)
                {
                    continue;
                }

                if (versionSpecified)
                {
                    // If an exact version is specified, check if it exists at this source
                    foreach (var package in packages)
                    {
                        if (package?.Identity?.Version == packageWithNuGetVersion.NuGetVersion)
                        {
                            return (packageWithNuGetVersion.NuGetVersion, repo);
                        }
                    }

                    continue;
                }

                foreach (var package in packages)
                {
                    var version = package.Identity.Version;
                    if (versionToDownload == null || version > versionToDownload)
                    {
                        versionToDownload = version;
                        downloadSourceRepository = repo;
                    }
                }
            }

            if (versionToDownload == null)
            {
                logger.LogError(Strings.Error_PackageDownload_VersionNotFound);
            }

            return (versionToDownload, downloadSourceRepository);
        }

        /// <summary>
        /// Builds the set of SourceRepository objects to use for a given package,
        /// applying package source mapping (when --source is not provided) and
        /// validating HTTP usage only on the *effective* sources.
        /// </summary>
        private static bool TryGetRepositoriesForPackage(
            string packageId,
            PackageDownloadArgs args,
            IReadOnlyList<PackageSource> allPackageSources,
            PackageSourceMapping? packageSourceMapping,
            IReadOnlyDictionary<string, SourceRepository> sourceRepositoriesMap,
            ILoggerWithColor logger,
            out List<SourceRepository> repositories)
        {
            List<PackageSource> effectiveSources;

            var sourceExplicitlyProvided = args.Sources?.Count > 0;
            if (sourceExplicitlyProvided || (packageSourceMapping != null && !packageSourceMapping.IsEnabled))
            {
                // --source given OR mapping disabled: use all provided sources as-is
                effectiveSources = [.. allPackageSources];
            }
            else
            {
                // Mapping enabled, no --source: try mapped names first
                var mappedNames = packageSourceMapping == null ? [] : packageSourceMapping.GetConfiguredPackageSources(packageId);

                // Build effective sources in the same order as mappedNames
                var mapped = mappedNames
                    .Select(n => allPackageSources.FirstOrDefault(ps =>
                        string.Equals(ps.Name, n, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // Only validate insecure sources when mapping produced something
                if (mapped.Count > 0)
                {
                    if (DetectAndReportInsecureSources(args.AllowInsecureConnections, mapped!, logger))
                    {
                        repositories = [];
                        return false;
                    }

                    effectiveSources = mapped!;
                }
                else
                {
                    // No mapping for this package: fall back to all sources
                    effectiveSources = [.. allPackageSources];
                }
            }

            // Convert effective sources to repositories
            repositories = new List<SourceRepository>(effectiveSources.Count);
            foreach (var src in effectiveSources)
            {
                repositories.Add(sourceRepositoriesMap[src.Name]);
            }

            return true;
        }

        private static async Task<bool> DownloadPackageAsync(
            string id,
            NuGetVersion version,
            SourceRepository repo,
            SourceCacheContext cache,
            ISettings settings,
            string outputDirectory,
            Common.ILogger logger,
            CancellationToken token)
        {
            var extractionContext = new PackageExtractionContext(
                PackageSaveMode.Defaultv3,
                PackageExtractionBehavior.XmlDocFileSaveMode,
                ClientPolicyContext.GetClientPolicy(settings, logger),
                logger);

            var resolver = new VersionFolderPathResolver(outputDirectory);
            var userPackageFolder = new NuGetv3LocalRepository(outputDirectory);

            // no-op if already installed
            if (userPackageFolder.Exists(id, version))
            {
                logger.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PackageDownloadCommand_AlreadyInstalled,
                    id,
                    version.ToNormalizedString(),
                    outputDirectory));

                return true;
            }

            var packageIdentity = new PackageIdentity(id, version);
            var provider = new SourceRepositoryDependencyProvider(sourceRepository: repo, logger: logger, cacheContext: cache, ignoreFailedSources: false, ignoreWarning: false);
            using var downloader = await provider.GetPackageDownloaderAsync(packageIdentity, cache, logger, token);
            bool success = await PackageExtractor.InstallFromSourceAsync(packageIdentity, downloader, resolver, extractionContext, token);

            if (!success)
            {
                logger.LogError(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PackageDownloadCommand_UnableToDownload,
                    id,
                    version.ToNormalizedString(),
                    repo.PackageSource.Source));
                return false;
            }

            return success;
        }

        private static IReadOnlyList<PackageSource> GetPackageSources(IList<string>? sources, IPackageSourceProvider sourceProvider)
        {
            IEnumerable<PackageSource> configuredSources = sourceProvider.LoadPackageSources()
                .Where(s => s.IsEnabled);

            if (sources != null && sources.Count > 0)
            {
                // Use sources specified on command line
                return [.. sources.Select(s => PackageSourceProviderExtensions.ResolveSource(configuredSources, s))];
            }

            return [.. configuredSources];
        }

        private static bool DetectAndReportInsecureSources(
            bool allowInsecureConnections,
            IEnumerable<PackageSource> packageSources,
            ILoggerWithColor logger)
        {
            if (!allowInsecureConnections)
            {
                var insecureSources = HttpSourcesUtility.GetDisallowedInsecureHttpSources([.. packageSources]);
                if (insecureSources.Any())
                {
                    logger.LogError(HttpSourcesUtility.BuildHttpSourceErrorMessage(insecureSources, "package download"));
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyDictionary<string, SourceRepository> GetSourceRepositories(IReadOnlyList<PackageSource> packageSources)
        {
            IEnumerable<Lazy<INuGetResourceProvider>> providers = Repository.Provider.GetCoreV3();
            Dictionary<string, SourceRepository> sourceRepositories = [];
            foreach (var source in packageSources)
            {
                sourceRepositories[source.Name] = Repository.CreateSource(providers, source, FeedType.Undefined);
            }

            return sourceRepositories;
        }
    }
}
