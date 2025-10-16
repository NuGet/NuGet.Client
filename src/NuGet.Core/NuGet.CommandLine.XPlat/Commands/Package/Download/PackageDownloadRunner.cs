// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            IEnumerable<PackageSource> packageSources = GetPackageSources(args.Sources, new PackageSourceProvider(settings));

            return await RunAsync(args, logger, packageSources, settings, token);
        }

        public static async Task<int> RunAsync(PackageDownloadArgs args, ILoggerWithColor logger, IEnumerable<PackageSource> packageSources, ISettings settings, CancellationToken token)
        {
            // Check for insecure sources
            if (DetectAndReportInsecureSources(args.AllowInsecureConnections, packageSources, logger))
            {
                return ExitCodeError;
            }

            string outputDirectory = args.OutputDirectory ?? Directory.GetCurrentDirectory();
            var cache = new SourceCacheContext();
            IEnumerable<SourceRepository> sourceRepositories = GetSourceRepositories(packageSources);
            bool downloadedAllSuccessfully = true;

            foreach (var package in args.Packages)
            {
                logger.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PackageDownloadCommand_Starting,
                    package.Id,
                    string.IsNullOrEmpty(package.NuGetVersion?.ToNormalizedString()) ? Strings.PackageDownloadCommand_LatestVersion : package.NuGetVersion.ToNormalizedString()));

                try
                {
                    (NuGetVersion version, SourceRepository downloadRepository) =
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

                    bool download = await DownloadPackageAsync(
                                        package.Id,
                                        version,
                                        downloadRepository,
                                        cache,
                                        settings,
                                        outputDirectory,
                                        logger,
                                        token);

                    if (download)
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

        internal static async Task<(NuGetVersion, SourceRepository)> ResolvePackageDownloadVersion(
            PackageWithNuGetVersion package,
            IEnumerable<SourceRepository> sourceRepositories,
            SourceCacheContext cache,
            ILoggerWithColor logger,
            bool includePrerelease,
            CancellationToken token)
        {
            NuGetVersion versionToDownload = null;
            SourceRepository downloadSourceRepository = null;
            bool versionSpecified = package.NuGetVersion != null;

            foreach (var repo in sourceRepositories)
            {
                var finder = await repo.GetResourceAsync<PackageMetadataResource>(token);
                var packages = await finder.GetMetadataAsync(
                    package.Id,
                    includePrerelease,
                    includeUnlisted: false,
                    sourceCacheContext: cache,
                    logger,
                    token);

                if (packages == null)
                {
                    continue;
                }

                var versions = packages?.Select(p => p.Identity.Version);
                if (versionSpecified)
                {
                    // If an exact version is specified, check if it exists at this source
                    foreach (var p in packages)
                    {
                        if (p?.Identity?.Version == package.NuGetVersion)
                        {
                            return (package.NuGetVersion, repo);
                        }
                    }

                    continue;
                }

                foreach (var p in packages)
                {
                    var v = p.Identity.Version;
                    if (versionToDownload is null || v > versionToDownload)
                    {
                        versionToDownload = v;
                    }
                }
            }

            if (versionToDownload == null)
            {
                logger.LogError(Strings.Error_PackageDownload_VersionNotFound);
            }

            return (versionToDownload, downloadSourceRepository);
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

            try
            {
                var finder = await repo.GetResourceAsync<FindPackageByIdResource>(token);
                bool installed = await PackageExtractor.InstallFromSourceAsync(
                repo.PackageSource.Source,
                new PackageIdentity(id, version),
                async (destination) =>
                {
                    using var nupkg = new MemoryStream();
                    bool ok = await finder.CopyNupkgToStreamAsync(
                        id, version, nupkg, cache, logger, token);

                    if (!ok) throw new InvalidOperationException("Package not found.");

                    nupkg.Position = 0;
                    await nupkg.CopyToAsync(destination, 81920);
                },
                resolver,
                extractionContext,
                token);

                if (installed)
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // Unable to download the package
                logger.LogError(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PackageDownloadCommand_UnableToDownload,
                    id,
                    version.ToNormalizedString(),
                    repo.PackageSource.Source));
                return false;
            }

            return false;
        }

        private static IEnumerable<PackageSource> GetPackageSources(IList<string> sources, IPackageSourceProvider sourceProvider)
        {
            IEnumerable<PackageSource> configuredSources = sourceProvider.LoadPackageSources()
                .Where(s => s.IsEnabled);

            IEnumerable<PackageSource> packageSources;

            if (sources != null && sources.Count > 0)
            {
                // Use sources specified on command line
                packageSources = sources
                    .Select(s => PackageSourceProviderExtensions.ResolveSource(configuredSources, s));
            }
            else
            {
                // Use all configured sources
                packageSources = configuredSources;
            }

            return packageSources;
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

        private static IEnumerable<SourceRepository> GetSourceRepositories(IEnumerable<PackageSource> packageSources)
        {
            IEnumerable<Lazy<INuGetResourceProvider>> providers = Repository.Provider.GetCoreV3();
            List<SourceRepository> sourceRepositories = [];
            foreach (var source in packageSources)
            {
                sourceRepositories.Add(Repository.CreateSource(providers, source, FeedType.Undefined));
            }

            return sourceRepositories;
        }
    }
}
