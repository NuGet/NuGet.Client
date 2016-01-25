// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    public class DownloadResourceV2 : DownloadResource
    {
        private readonly IPackageRepository V2Client;

        public DownloadResourceV2(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public DownloadResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            Configuration.ISettings settings,
            NuGet.Logging.ILogger logger,
            CancellationToken token)
        {
            // settings are not used here, since, global packages folder are not used for v2 sources
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            string displayUri = V2Client.Source;

            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var sourcePackage = identity as SourcePackageDependencyInfo;
                        var repository = V2Client as DataServicePackageRepository;

                        bool isFromUri = repository != null
                            && sourcePackage?.PackageHash != null
                            && sourcePackage?.DownloadUri != null;

                        if (isFromUri)
                        {
                            // If this is a SourcePackageDependencyInfo object with everything populated
                            // and it is from an online source, use the machine cache and download it using the
                            // given url.
                            return DownloadFromUrl(sourcePackage, repository, logger, token);
                        }
                        else
                        {
                            // Look up the package from the id and version and download it.
                            return DownloadFromIdentity(identity, V2Client, logger, token);
                        }
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException && i < 2)
                    {
                        string message = string.Format(Strings.Warning_ErrorDownloading,
                            identity,
                            displayUri,
                            ExceptionUtilities.DisplayMessage(ex));

                        logger.LogWarning(message);
                    }
                    catch (Exception ex)
                    {
                        throw new FatalProtocolException(ex);
                    }
                }

                // This line cannot be reached, but the compiler doesn't understand it
                throw new InvalidOperationException("Can never reach here.");
            });
        }

        private DownloadResourceResult DownloadFromUrl(
            SourcePackageDependencyInfo package,
            DataServicePackageRepository repository,
            NuGet.Logging.ILogger logger,
            CancellationToken token)
        {
            IPackage newPackage = null;
            var version = SemanticVersion.Parse(package.Version.ToString());
            var cacheRepository = MachineCache.Default;

            try
            {
                // Try finding the package in the machine cache
                var localPackage = cacheRepository.FindPackage(package.Id, version)
                    as OptimizedZipPackage;

                // Validate the package matches the hash
                if (localPackage != null
                    && localPackage.IsValid
                    && MatchPackageHash(localPackage, package.PackageHash))
                {
                    newPackage = localPackage;
                }
            }
            catch
            {
                // Ignore cache failures here to match NuGet.Core
                // The bad package will be deleted and replaced during the download.
            }

            // If the local package does not exist in the cache download it from the source
            if (newPackage == null)
            {
                newPackage = DownloadToMachineCache(
                    cacheRepository,
                    package,
                    repository,
                    package.DownloadUri,
                    logger,
                    token);
            }

            // Read the package from the machine cache
            if (newPackage != null)
            {
                return new DownloadResourceResult(newPackage.GetStream());
            }

            return null;
        }

        private DownloadResourceResult DownloadFromIdentity(
            PackageIdentity identity,
            IPackageRepository repository,
            NuGet.Logging.ILogger logger,
            CancellationToken token)
        {
            var version = SemanticVersion.Parse(identity.Version.ToString());
            var dataServiceRepo = repository as DataServicePackageRepository;

            if (dataServiceRepo != null)
            {
                // Clone the repo to allow for concurrent calls
                var sourceUri = new Uri(dataServiceRepo.Source);
                dataServiceRepo = new DataServicePackageRepository(sourceUri);

                var package = dataServiceRepo.FindPackage(identity.Id, version);
                var dataServicePackage = package as DataServicePackage;

                if (dataServicePackage != null)
                {
                    token.ThrowIfCancellationRequested();

                    // For online sources get the url and retrieve it with cancel support
                    var url = dataServicePackage.DownloadUrl;

                    var downloadedPackage = DownloadToMachineCache(
                        MachineCache.Default,
                        identity,
                        dataServiceRepo,
                        url,
                        logger,
                        token);

                    if (downloadedPackage != null)
                    {
                        return new DownloadResourceResult(downloadedPackage.GetStream());
                    }
                }
            }
            else
            {
                var package = repository.FindPackage(identity.Id, version);

                if (package != null)
                {
                    // Use a folder reader for unzipped repos
                    if (repository is UnzippedPackageRepository)
                    {
                        var packagePath = Path.Combine(repository.Source, identity.Id + "." + version);
                        var directoryInfo = new DirectoryInfo(packagePath);
                        if (directoryInfo.Exists)
                        {
                            return new DownloadResourceResult(
                                package.GetStream(),
                                new PackageFolderReader(directoryInfo));
                        }
                    }

                    return new DownloadResourceResult(package.GetStream());
                }
            }

            return null;
        }

        /// <summary>
        /// True if the given package matches hash
        /// </summary>
        private static bool MatchPackageHash(IPackage package, string hash)
        {
            var hashProvider = new CryptoHashProvider("SHA512");

            return package != null && package.GetHash(hashProvider).Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        private IPackage DownloadToMachineCache(
            IPackageCacheRepository cacheRepository,
            PackageIdentity package,
            DataServicePackageRepository repository,
            Uri downloadUri,
            NuGet.Logging.ILogger logger,
            CancellationToken token)
        {
            var packageName = new PackageNameWrapper(package);
            var version = SemanticVersion.Parse(package.Version.ToString());
            IPackage newPackage = null;

            FileInfo tmpFile = null;
            FileStream tmpFileStream = null;

            // Create a v2 http client
            var downloadClient = new HttpClient(downloadUri)
            {
                UserAgent = UserAgent.UserAgentString
            };

            EventHandler<ProgressEventArgs> progressHandler = (sender, progress) =>
            {
                // Throw if this was canceled. This will stop the download.
                token.ThrowIfCancellationRequested();
            };

            Exception downloadException = null;

            Action<Stream> downloadAction = (stream) =>
            {
                try
                {
                    repository.PackageDownloader.ProgressAvailable += progressHandler;

                    logger.LogVerbose($"  GET: {downloadUri}");
                    repository.PackageDownloader.DownloadPackage(downloadClient, packageName, stream);
                }
                catch (Exception ex) when (ex is OperationCanceledException ||
                                           ex is IOException && ex.InnerException is SocketException)
                {
                    // The task was canceled. To avoid writing a partial file to the machine cache
                    // we need to clear out the current tmp file stream so that it will be ignored.
                    stream.SetLength(0);

                    // If the machine cache is using the physical file system we can find the
                    // path of temp file and clean it up. Otherwise NuGet.Core will just leave the temp file.
                    tmpFileStream = stream as FileStream;
                    if (tmpFileStream != null)
                    {
                        tmpFile = new FileInfo(tmpFileStream.Name);
                    }

                    downloadException = ex;
                }
                catch (Exception ex)
                {
                    downloadException = ex;
                }
                finally
                {
                    repository.PackageDownloader.ProgressAvailable -= progressHandler;
                }
            };

            // We either do not have a package available locally or they are invalid.
            // Download the package from the server.
            if (cacheRepository.InvokeOnPackage(package.Id, version,
                (stream) => downloadAction(stream)))
            {
                if (!token.IsCancellationRequested)
                {
                    newPackage = cacheRepository.FindPackage(package.Id, version);
                    Debug.Assert(newPackage != null);
                }
            }

            // After the stream is no longer in use, delete the tmp file
            // NuGet.Core does not properly clean these up since it does not have cancel support.
            if (tmpFile != null && token.IsCancellationRequested && tmpFile.Exists)
            {
                try
                {
                    tmpFile.Delete();
                }
                catch
                {
                    // Ignore exceptions for tmp file clean up
                }
            }

            if (downloadException != null)
            {
                throw downloadException;
            }

            return newPackage;
        }
    }
}
