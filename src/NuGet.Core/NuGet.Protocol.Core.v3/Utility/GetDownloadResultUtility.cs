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

            //TODO: NuGet/Home#1406 This code should respect -NoCache option and not read packages from the global packages folder
            //Note cacheContext.NoCache indicates that packages are not written to the global packages folder
            packageFromGlobalPackages = GlobalPackagesFolderUtility.GetPackage(identity, settings);

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

                            if (cacheContext.NoCache)
                            {
                                return await AddPackageDirectAsync(
                                    identity,
                                    packageStream,
                                    settings,
                                    cacheContext.GeneratedTempFolder,
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

            var versionFolderPathContext = new VersionFolderPathContext(
                packageIdentity,
                PackagesDirectory,
                logger,
                packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Nuspec,
                xmlDocFileSaveMode: PackageExtractionBehavior.XmlDocFileSaveMode);

            await PackageExtractor.InstallFromSourceAsync(
                stream => packageStream.CopyToAsync(stream, bufferSize: 8192, cancellationToken: token),
                versionFolderPathContext,
                token: token);

            var defaultPackagePathResolver = new VersionFolderPathResolver(PackagesDirectory);
            var path = defaultPackagePathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);

            if (File.Exists(path))
            {
                Stream stream = null;
                PackageReaderBase packageReader = null;
                try
                {
                    stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    packageReader = new PackageArchiveReader(path);
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
