// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            // Custom logger for package downloads.
            // It wraps the provided logger and uses verbosity levels to determine which messages to log.
            XPlatUtility.ConfigureProtocol();
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(logger, !args.Interactive);
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                args.ConfigFile,
                new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var packageSources = GetPackageSources(args.Sources, sourceProvider);

            return await RunAsync(args, logger, packageSources, settings, token);
        }

        public static async Task<int> RunAsync(PackageDownloadArgs args, ILoggerWithColor logger, IEnumerable<PackageSource> packageSources, ISettings settings, CancellationToken token)
        {
            // Check for insecure sources
            if (!args.AllowInsecureConnections)
            {
                var insecureSources = packageSources.Where(source => source.SourceUri.AbsoluteUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

                if (insecureSources.Any())
                {
                    if (insecureSources.Count() == 1)
                    {
                        logger.LogError(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Error_HttpServerUsage,
                            "download",
                           insecureSources.First()));
                    }
                    else
                    {
                        logger.LogError(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Error_HttpServerUsage_MultipleSources,
                            "download",
                            string.Join(", ", insecureSources)));
                    }

                    return ExitCodeError;
                }
            }

            var cache = new SourceCacheContext()
            {
                NoCache = true,
                DirectDownload = true
            };

            bool downloadedAllSuccessfully = true;

            foreach (var package in args.Packages)
            {
                logger.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PkgDownload_Starting,
                    package.Id,
                    string.IsNullOrEmpty(package.VersionRange?.ToNormalizedString()) ? "latest version" : package.VersionRange.ToNormalizedString()));

#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    (NuGetVersion version, SourceRepository downloadRepository) = await ResolvePackageDownloadVersion(package, packageSources, cache, logger, args.IncludePrerelease, token);

                    if (version == null)
                    {
                        // Unable to find a valid version
                        downloadedAllSuccessfully &= false;
                        continue;
                    }

                    bool download = await InstallPackageAsync(
                                        package.Id,
                                        version,
                                        downloadRepository,
                                        cache,
                                        settings,
                                        args.OutputDirectory,
                                        logger,
                                        token);

                    if (download)
                    {
                        logger.LogMinimal(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.PkgDownload_Succeeded,
                            package.Id,
                            version,
                            args.OutputDirectory));
                    }
                    else
                    {
                        logger.LogError(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.PkgDownload_Failed,
                            package.Id,
                            version));

                        downloadedAllSuccessfully &= false;
                    }
                }
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
            Package package,
            IEnumerable<PackageSource> packageSources,
            SourceCacheContext cache,
            ILoggerWithColor logger,
            bool includePrerelease,
            CancellationToken token)
        {
            // Determine if an exact version is requested.
            // If the original string contains a comma, it's a range.
            bool isExactVersionRequested =
                package.VersionRange != null &&
                !string.IsNullOrEmpty(package.VersionRange.OriginalString) &&
                !package.VersionRange.OriginalString.Contains(",");
            NuGetVersion requestedExactVersion = package.VersionRange?.MinVersion;

            NuGetVersion versionToDownload = null;
            SourceRepository downloadSourceRepository = null;

            bool pickLatest = false;

            if (package.VersionRange == null)
            {
                // If the version is not defined, pick the latest
                pickLatest = true;
            }

            foreach (var source in packageSources)
            {
                var repo = Repository.Factory.GetCoreV3(source);
                var finder = await repo.GetResourceAsync<FindPackageByIdResource>(token);
                var versions = await finder.GetAllVersionsAsync(package.Id, cache, logger, token);

                if (isExactVersionRequested)
                {
                    // If an exact version is requested, check if the version exists at this source
                    if (versions != null && versions.Contains(requestedExactVersion))
                    {
                        versionToDownload = requestedExactVersion;
                        downloadSourceRepository = repo;
                        return (versionToDownload, downloadSourceRepository);
                    }
                    else
                    {
                        // continue to the next source
                        continue;
                    }
                }

                // If a version range is specified, find the best match at this source
                var candidates = versions?
                    .Where(v => (package.VersionRange == null || package.VersionRange.Satisfies(v)) && (includePrerelease || !v.IsPrerelease))
                    .OrderByDescending(v => v);

                var candidate = pickLatest ? candidates?.FirstOrDefault() : candidates?.LastOrDefault();

                if (candidate != null && (versionToDownload == null || pickLatest ? candidate > versionToDownload : candidate < versionToDownload))
                {
                    versionToDownload = candidate;
                    downloadSourceRepository = repo;
                }
            }

            if (versionToDownload == null)
            {
                logger.LogError("Unable to find a valid package version");
            }

            return (versionToDownload, downloadSourceRepository);
        }

        private static async Task<bool> InstallPackageAsync(
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
                        Strings.PkgDownload_AlreadyInstalled,
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
                    Strings.PkgDownload_UnableToDownload,
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
    }
}
