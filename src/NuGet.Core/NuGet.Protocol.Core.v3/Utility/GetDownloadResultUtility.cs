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

            var packagesFolder = PackagesDirectory;

            var targetPath = PackagesDirectory;
            var packageName = packageIdentity.ToString();
            var targetNupkg = Path.Combine(targetPath, packageName);

            logger.LogVerbose(
                $"Acquiring lock for the installation of {packageIdentity.Id} {packageIdentity.Version}");

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLockedAsync(targetNupkg,
                action: async cancellationToken =>
                {
                // If this is the first process trying to install the target nupkg, go ahead
                // After this process successfully installs the package, all other processes
                // waiting on this lock don't need to install it again.
                if (!File.Exists(targetNupkg))
                    {
                        logger.LogVerbose(
                            $"Acquired lock for the installation of {packageIdentity.Id} {packageIdentity.Version}");

                        logger.LogMinimal(string.Format(
                            CultureInfo.CurrentCulture,
                            Packaging.Strings.Log_InstallingPackage,
                            packageIdentity.Id,
                            packageIdentity.Version));

                        cancellationToken.ThrowIfCancellationRequested();

                        if (Directory.Exists(targetPath))
                        {
                        // If we had a broken restore, clean out the files first
                        var info = new DirectoryInfo(targetPath);

                            foreach (var file in info.GetFiles())
                            {
                                file.Delete();
                            }

                            foreach (var dir in info.GetDirectories())
                            {
                                dir.Delete(true);
                            }
                        }
                        else
                        {
                            Directory.CreateDirectory(targetPath);
                        }

                        var targetTempNupkg = Path.Combine(targetPath, Path.GetRandomFileName());

                        // Extract the nupkg
                        using (var nupkgStream = new FileStream(
                            targetTempNupkg,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete,
                            bufferSize: 4096,
                            useAsync: true))
                        {
                            await packageStream.CopyToAsync(nupkgStream, bufferSize: 8192, cancellationToken: token);
                            nupkgStream.Seek(0, SeekOrigin.Begin);

                            using (var packageReader = new PackageArchiveReader(nupkgStream))
                            {
                            }
                        }

                        // Now rename the tmp file
                        File.Move(targetTempNupkg, targetNupkg);

                        logger.LogVerbose($"Completed direct download of {packageIdentity.Id} {packageIdentity.Version}");
                    }
                    else
                    {
                        logger.LogVerbose("Lock not required - Package already installed "
                                            + $"{packageIdentity.Id} {packageIdentity.Version}");
                    }

                    return 0;
                },
                token: token);

            var package = GetDirectPackage(packageIdentity, PackagesDirectory, packageName);
            Debug.Assert(package.PackageStream.CanSeek);
            Debug.Assert(package.PackageReader != null);

            return package;
        }

        private static DownloadResourceResult GetDirectPackage(PackageIdentity packageIdentity, string PackagesDirectory, string packageName)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            string path = Path.Combine(PackagesDirectory, packageName);

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
