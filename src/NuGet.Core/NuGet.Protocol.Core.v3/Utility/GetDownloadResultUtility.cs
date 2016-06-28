// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using System.Diagnostics;

namespace NuGet.Protocol
{
    public static class GetDownloadResultUtility
    {
        public static async Task<DownloadResourceResult> GetDownloadResultAsync(
           HttpSource client,
           PackageIdentity identity,
           Uri uri,
           ISettings settings,
           SourceCacheContext cacheContext,
           ILogger logger,
           CancellationToken token)
        {
            // Uri is not null, so the package exists in the source
            // Now, check if it is in the global packages folder, before, getting the package stream

            DownloadResourceResult packageFromGlobalPackages = null;

            if (!cacheContext.NoCache)
            {
                packageFromGlobalPackages = GlobalPackagesFolderUtility.GetPackage(identity, settings);
            }

            if (packageFromGlobalPackages != null)
            {
                return packageFromGlobalPackages;
            }

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    return await client.ProcessStreamAsync(
                        new HttpSourceRequest(uri, logger)
                        {
                            IgnoreNotFounds = true
                        },
                        async packageStream =>
                        {
                            if (packageStream == null)
                            {
                                return new DownloadResourceResult(DownloadResourceResultStatus.NotFound);
                            }

                            if (cacheContext.NoCaching)
                            {
                                return await AddPackageDirectAsync(
                                identity,
                                packageStream,
                                settings,
                                cacheContext.NoCachingDirectory,
                                cacheContext.NoCachingPackageSaveMode,
                                logger,
                                token);
                            }
                            else
                            {
                                return await GlobalPackagesFolderUtility.AddPackageAsync(
                                identity,
                                packageStream,
                                settings,
                                logger,
                                token);
                            }
                        },
                        logger,
                        token);
                }
                catch (OperationCanceledException)
                {
                    return new DownloadResourceResult(DownloadResourceResultStatus.Cancelled);
                }
                catch (Exception ex) when ((
                        (ex is IOException && ex.InnerException is SocketException)
                        || ex is TimeoutException)
                    && i < 2)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ErrorDownloading, identity, uri)
                        + Environment.NewLine
                        + ExceptionUtilities.DisplayMessage(ex);
                    logger.LogWarning(message);
                }
                catch (Exception ex)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ErrorDownloading, identity, uri);
                    logger.LogError(message + Environment.NewLine + ExceptionUtilities.DisplayMessage(ex));

                    throw new FatalProtocolException(message, ex);
                }
            }

            throw new InvalidOperationException("Reached an unexpected point in the code");
        }

        private static async Task<DownloadResourceResult> AddPackageDirectAsync(PackageIdentity packageIdentity,
            Stream packageStream,
            ISettings settings,
            string PackagesDirectory,
            PackageSaveMode packageSaveMode,
            ILogger logger,
            CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (packageStream == null)
            {
                throw new ArgumentNullException(nameof(packageStream));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var packagesFolder = PackagesDirectory;

            var versionFolderPathContext = new VersionFolderPathContext(
                packageIdentity,
                packagesFolder,
                logger,
                packageSaveMode: packageSaveMode,
                xmlDocFileSaveMode: PackageExtractionBehavior.XmlDocFileSaveMode);

            await PackageExtractor.InstallFromSourceAsync(
                stream => packageStream.CopyToAsync(stream, bufferSize: 8192, cancellationToken: token),
                versionFolderPathContext,
                token: token);

            var package = GetDirectPackage(packageIdentity, settings, PackagesDirectory, packageSaveMode);
            Debug.Assert(package.PackageStream.CanSeek);
            Debug.Assert(package.PackageReader != null);

            return package;
        }

        private static DownloadResourceResult GetDirectPackage(PackageIdentity packageIdentity, ISettings settings,
            string PackagesDirectory, PackageSaveMode packageSaveMode)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var defaultPackagePathResolver = new VersionFolderPathResolver(PackagesDirectory);

            string path = null;
            if ((packageSaveMode & PackageSaveMode.Nupkg) == PackageSaveMode.Nupkg)
            {
                path = defaultPackagePathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);
            }
            else if ((packageSaveMode & PackageSaveMode.Nuspec) == PackageSaveMode.Nuspec)
            {
                path = defaultPackagePathResolver.GetManifestFilePath(packageIdentity.Id, packageIdentity.Version);
            }

            if (File.Exists(path))
            {
                var installPath = defaultPackagePathResolver.GetInstallPath(
                    packageIdentity.Id,
                    packageIdentity.Version);

                Stream stream = null;
                PackageReaderBase packageReader = null;
                try
                {
                    stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    packageReader = new PackageFolderReader(installPath);
                    return new DownloadResourceResult(stream, packageReader);
                }
                catch
                {
                    if (stream != null)
                    {
                        stream.Dispose();
                    }

                    if (packageReader != null)
                    {
                        packageReader.Dispose();
                    }

                    throw;
                }
            }

            return null;
        }

    }
}
