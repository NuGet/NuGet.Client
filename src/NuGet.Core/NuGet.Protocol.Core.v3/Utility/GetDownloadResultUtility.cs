// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public static class GetDownloadResultUtility
    {
        private const int BufferSize = 8192;
        private const string DirectDownloadExtension = ".nugetdirectdownload";
        private const string DirectDownloadPattern = "*" + DirectDownloadExtension;

        public static async Task<DownloadResourceResult> GetDownloadResultAsync(
           HttpSource client,
           PackageIdentity identity,
           Uri uri,
           PackageDownloadContext downloadContext,
           string globalPackagesFolder,
           ILogger logger,
           CancellationToken token)
        {
            // This code should respect -NoCache option and not read packages from the global
            // packages folder: https://github.com/NuGet/Home/issues/1406
            var packageFromGlobalPackages = GlobalPackagesFolderUtility.GetPackage(
                identity,
                globalPackagesFolder);

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

                            if (downloadContext.DirectDownload)
                            {
                                return await DirectDownloadAsync(
                                    identity,
                                    packageStream,
                                    downloadContext,
                                    token);
                            }
                            else
                            {
                                return await GlobalPackagesFolderUtility.AddPackageAsync(
                                    identity,
                                    packageStream,
                                    globalPackagesFolder,
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

        /// <summary>
        /// Allow explicit clean-up of direct download files. This is important because although direct downloads are
        /// opened with the <see cref="FileOptions.DeleteOnClose"/> option, some systems (e.g. Linux) do not perform
        /// the delete if the process dies. Additionally, if the system dies before the process dies (e.g. loss of
        /// power), the direct download files will be left over.
        /// </summary>
        /// <param name="downloadContext">The download context.</param>
        public static void CleanUpDirectDownloads(PackageDownloadContext downloadContext)
        {
            foreach (var file in Directory.EnumerateFiles(
                downloadContext.DirectDownloadDirectory,
                DirectDownloadPattern,
                SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e) when (e is UnauthorizedAccessException ||
                                          e is IOException)
                {
                    // Ignore exceptions indicating the file has permissions protecting it or if the file is in use.
                }
            }
        }

        private static async Task<DownloadResourceResult> DirectDownloadAsync(
            PackageIdentity packageIdentity,
            Stream packageStream,
            PackageDownloadContext downloadContext,
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

            if (downloadContext == null)
            {
                throw new ArgumentNullException(nameof(downloadContext));
            }

            // Build a file name for the package that is being downloaded. The caller provided the directory that
            // should be written to, but a random file name is used to avoid the necessity of locking. The caller
            // provides a directory so that a high performance or local drive can be used (instead of the %TEMP%
            // directory which can be different from the extraction location). The random file name is not just a
            // performance optimization. This also means that future versions of NuGet can co-exist with this
            // extraction code since the random component is specifically designed to avoid collisions.
            var randomComponent = Path.GetRandomFileName();
            var fileName = $"{randomComponent}{DirectDownloadExtension}";
            var directDownloadPath = Path.Combine(downloadContext.DirectDownloadDirectory, fileName);

            FileStream fileStream = null;

            try
            {
                Directory.CreateDirectory(downloadContext.DirectDownloadDirectory);

                // Use DeleteOnClose when opening this stream since this file is just used for for the package
                // extraction. This file is meant to be ephemeral because package extraction does not always result
                // in a .nupkg on disk. Even if a .nupkg is in the extraction result (via PackageSaveMode.Nupkg), it
                // is not written to disk first, as is happening here.
                fileStream = new FileStream(
                   directDownloadPath,
                   FileMode.Create,
                   FileAccess.ReadWrite,
                   FileShare.Read,
                   BufferSize,
                   FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                await packageStream.CopyToAsync(fileStream, BufferSize, token);

                fileStream.Seek(0, SeekOrigin.Begin);

                return new DownloadResourceResult(fileStream);
            }
            catch
            {
                fileStream?.Dispose();

                throw;
            }
        }
    }
}
