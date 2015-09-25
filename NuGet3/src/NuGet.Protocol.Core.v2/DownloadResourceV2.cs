// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        public override async Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            SourcePackageDependencyInfo package,
            Configuration.ISettings settings,
            CancellationToken token)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return await Task.Run(async () =>
                {
                    var dataServiceRepo = V2Client as DataServicePackageRepository;
                    IPackage newPackage = null;

                    // Using the below code the machine cache can be used with a hash. The fallback
                    // is to just download the package.
                    //
                    // If this is a SourcePackageDependencyInfo object with everything populated 
                    // and it is from an online source, use the machine cache and download it using the
                    // given url.
                    // If this info is not provided fallback to the old method.
                    if (dataServiceRepo != null
                            && !string.IsNullOrEmpty(package.PackageHash)
                            && package.DownloadUri != null)
                    {
                        var version = SemanticVersion.Parse(package.Version.ToString());
                        var cacheRepository = MachineCache.Default;

                        try
                        {
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
                                    dataServiceRepo, 
                                    package.DownloadUri,
                                    token);
                            }

                            // Read the package from the machine cache
                            if (newPackage != null)
                            {
                                return new DownloadResourceResult(newPackage.GetStream());
                            }

                            return null;
                        }
                        catch (Exception ex)
                        {
                            throw new NuGetProtocolException(Strings.FormatProtocol_FailedToDownloadPackage(package, V2Client.Source), ex);
                        }
                    }

                    // package did not contain the needed info, fall back to looking up the package
                    return await GetDownloadResourceResultAsync(package, settings, token);
                });
        }

        public override Task<DownloadResourceResult> GetDownloadResourceResultAsync(PackageIdentity identity,
            Configuration.ISettings settings,
            CancellationToken token)
        {
            // settings are not used here, since, global packages folder are not used for v2 sources

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var version = SemanticVersion.Parse(identity.Version.ToString());

                try
                {
                    var package = V2Client.FindPackage(identity.Id, version);

                    token.ThrowIfCancellationRequested();

                    if (package != null)
                    {
                        var dataServicePackage = package as DataServicePackage;
                        var dataServiceRepo = V2Client as DataServicePackageRepository;

                        if (dataServicePackage != null && dataServiceRepo != null)
                        {
                            // For online sources get the url and retrieve it with cancel support
                            var url = dataServicePackage.DownloadUrl;

                            var downloadedPackage = DownloadToMachineCache(
                                MachineCache.Default,
                                identity,
                                dataServiceRepo,
                                url,
                                token);

                            if (downloadedPackage != null)
                            {
                                return new DownloadResourceResult(downloadedPackage.GetStream());
                            }
                        }
                        else
                        {
                            // Use a folder reader for unzipped repos
                            if (V2Client is UnzippedPackageRepository)
                            {
                                var packagePath = Path.Combine(V2Client.Source, identity.Id + "." + version);
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
                catch (Exception ex)
                {
                    throw new NuGetProtocolException(Strings.FormatProtocol_FailedToDownloadPackage(identity, V2Client.Source), ex);
                }
            });
        }

        /// <summary>
        /// True if the given package matches hash
        /// </summary>
        private bool MatchPackageHash(IPackage package, string hash)
        {
            var hashProvider = new CryptoHashProvider("SHA512");

            return package != null && package.GetHash(hashProvider).Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        private IPackage DownloadToMachineCache(
            IPackageCacheRepository cacheRepository,
            PackageIdentity package,
            DataServicePackageRepository dataServiceRepo,
            Uri downloadUri,
            CancellationToken token)
        {
            var packageName = new PackageNameWrapper(package);
            var version = SemanticVersion.Parse(package.Version.ToString());
            IPackage newPackage = null;

            FileInfo tmpFile = null;

            var downloadClient = new HttpClient(downloadUri)
            {
                UserAgent = UserAgent.UserAgentString
            };

            EventHandler<ProgressEventArgs> progressHandler = (sender, progress) =>
            {
                // Throw if this was canceled. This will stop the download.
                token.ThrowIfCancellationRequested();
            };

            Action<Stream> downloadAction = (stream) =>
            {
                try
                {
                    dataServiceRepo.PackageDownloader.ProgressAvailable += progressHandler;
                    dataServiceRepo.PackageDownloader.DownloadPackage(downloadClient, packageName, stream);
                }
                catch (OperationCanceledException)
                {
                    // The task was canceled. To avoid writing a partial file to the machine cache 
                    // we need to clear out the current tmp file stream so that it will be ignored.
                    stream.SetLength(0);

                    // If the machine cache is using the physical file system we can find the 
                    // path of temp file and clean it up. Otherwise NuGet.Core will just leave the temp file.
                    var fileStream = stream as FileStream;
                    if (fileStream != null)
                    {
                        tmpFile = new FileInfo(fileStream.Name);
                    }
                }
                finally
                {
                    dataServiceRepo.PackageDownloader.ProgressAvailable -= progressHandler;
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

            // After the stream is no longer in use, delete the tmp file if it still exists after a canceled task
            // NuGet.Core does not properly clean these up since it does not have cancel support.
            if (tmpFile != null && token.IsCancellationRequested && tmpFile.Exists)
            {
                try
                {
                    tmpFile.Delete();
                }
                catch
                {
                    // Ignore exceptions
                    Debug.Fail("Unable to remove tmp file from v2 download");
                }
            }

            return newPackage;
        }
    }
}
