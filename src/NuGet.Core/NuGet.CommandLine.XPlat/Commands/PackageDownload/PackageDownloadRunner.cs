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

namespace NuGet.CommandLine.XPlat.Commands.PackageDownload
{
    internal static class PackageDownloadRunner
    {
        public static async Task<int> RunAsync(PackageDownloadArgs args, CancellationToken token)
        {
            // Custom logger for package downloads.
            // It wraps the provided logger and uses verbosity levels to determine which messages to log.
            var logger = new PackageDownloadLogger(args.Logger, args.Verbosity);

            DefaultCredentialServiceUtility.SetupDefaultCredentialService(logger, !args.Interactive);
            ISettings settings = Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                args.ConfigFile,
                new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            var packageSources = GetPackageSources(args.Sources, sourceProvider);

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

                    return ExitCodes.Error;
                }
            }

            var cache = new SourceCacheContext()
            {
                NoCache = true,
                DirectDownload = true
            };

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                logger.LogMinimal(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PkgDownload_Starting,
                        args.PackageId,
                        string.IsNullOrEmpty(args.Version) ? "latest version" : args.Version));

                bool download = await InstallPackageAsync(
                    packageSources,
                    cache,
                    args,
                    settings,
                    logger,
                    token);

                if (download)
                {
                    logger.LogMinimal(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PkgDownload_Succeeded,
                        args.PackageId,
                        string.IsNullOrEmpty(args.Version) ? "latest version" : args.Version,
                        args.OutputDirectory));

                    return ExitCodes.Success;
                }
                else
                {
                    logger.LogError(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PkgDownload_Failed,
                        args.PackageId,
                        string.IsNullOrEmpty(args.Version) ? "latest version" : args.Version));

                    return ExitCodes.Error;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return ExitCodes.Error;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private static async Task<bool> InstallPackageAsync(
            IEnumerable<PackageSource> sources,
            SourceCacheContext cache,
            PackageDownloadArgs args,
            ISettings settings,
            Common.ILogger logger,
            CancellationToken token)
        {
            bool versionDefined = !string.IsNullOrEmpty(args.Version);
            NuGetVersion versionToDownload = null;
            SourceRepository downloadSourceRepository = null;

            if (versionDefined)
            {
                versionToDownload = NuGetVersion.Parse(args.Version);
            }

            var extractionContext = new PackageExtractionContext(
                PackageSaveMode.Defaultv3,
                PackageExtractionBehavior.XmlDocFileSaveMode,
                ClientPolicyContext.GetClientPolicy(settings, logger),
                logger);

            var resolver = new VersionFolderPathResolver(args.OutputDirectory);
            var userPackageFolder = new NuGetv3LocalRepository(args.OutputDirectory);

            // no-op if already installed
            if (versionDefined && userPackageFolder.Exists(args.PackageId, versionToDownload))
            {
                logger.LogMinimal(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PkgDownload_AlreadyInstalled,
                        args.PackageId,
                        versionToDownload.ToNormalizedString(),
                        args.OutputDirectory));

                return true;
            }

            // Try each source until we can download and install the requested version
            foreach (var source in sources)
            {
                var repo = Repository.Factory.GetCoreV3(source);
                var finder = await repo.GetResourceAsync<FindPackageByIdResource>(token);

                if (versionDefined)
                {
                    // download the defined version at the first source that has it
                    PackageIdentity identity = new PackageIdentity(args.PackageId, versionToDownload);

                    try
                    {
                        bool installed = await PackageExtractor.InstallFromSourceAsync(
                        source.Source,
                        identity,
                        async (destination) =>
                        {
                            using var nupkg = new MemoryStream();
                            bool ok = await finder.CopyNupkgToStreamAsync(
                                identity.Id, identity.Version, nupkg, cache, logger, token);

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

                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                        // Package not found at this source, try the next one
                        continue;
                    }
                }
                else
                {
                    // If the version is not defined
                    // update the version to download value to be the latest
                    var versions = await finder.GetAllVersionsAsync(args.PackageId, cache, logger, token);
                    var candidate = versions?
                        .Where(v => args.IncludePrerelease || !v.IsPrerelease)
                        .OrderByDescending(v => v)
                        .FirstOrDefault();

                    if (candidate != null && (versionToDownload == null || candidate > versionToDownload))
                    {
                        versionToDownload = candidate;
                        downloadSourceRepository = repo;
                    }
                }
            }

            // If the version was not defined, and we found a version across one of the sources, install it now
            if (!versionDefined && versionToDownload != null && downloadSourceRepository != null)
            {
                // no-op if already installed
                if (userPackageFolder.Exists(args.PackageId, versionToDownload))
                {
                    logger.LogInformation(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.PkgDownload_AlreadyInstalled,
                            args.PackageId,
                            versionToDownload.ToNormalizedString(),
                            args.OutputDirectory));

                    return true;
                }

                // download the latest version found across all sources
                PackageIdentity identity = new PackageIdentity(args.PackageId, versionToDownload);
                var findPackageByIdResource = await downloadSourceRepository.GetResourceAsync<FindPackageByIdResource>(token);

                try
                {
                    return await PackageExtractor.InstallFromSourceAsync(
                    downloadSourceRepository.PackageSource.Source,
                    identity,
                    async (destination) =>
                    {
                        using var nupkg = new MemoryStream();
                        bool ok = await findPackageByIdResource.CopyNupkgToStreamAsync(
                            identity.Id, identity.Version, nupkg, cache, logger, token);
                        if (!ok) throw new InvalidOperationException("Package not found.");
                        nupkg.Position = 0;
                        await nupkg.CopyToAsync(destination, 81920);
                    },
                    resolver,
                    extractionContext,
                    token);
                }
                catch (InvalidOperationException)
                {
                    // install failed
                    return false;
                }
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
