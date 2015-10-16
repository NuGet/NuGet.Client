using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    public static class GlobalPackagesFolderUtility
    {
        public static DownloadResourceResult GetPackage(PackageIdentity packageIdentity, ISettings settings)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
            var defaultPackagePathResolver = new VersionFolderPathResolver(
                globalPackagesFolder,
                normalizePackageId: false);

            var hashPath = defaultPackagePathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);

            if (File.Exists(hashPath))
            {
                var installPath = defaultPackagePathResolver.GetInstallPath(
                    packageIdentity.Id,
                    packageIdentity.Version);

                var nupkgPath = defaultPackagePathResolver.GetPackageFilePath(
                    packageIdentity.Id,
                    packageIdentity.Version);

                Stream stream = null;
                PackageReaderBase packageReader = null;
                try
                {
                    stream = File.Open(nupkgPath, FileMode.Open, FileAccess.Read, FileShare.Read);
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

        public static async Task<DownloadResourceResult> AddPackageAsync(PackageIdentity packageIdentity,
            Stream packageStream,
            ISettings settings,
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

            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

            // The following call adds it to the global packages folder.
            // Addition is performed using ConcurrentUtils, such that,
            // multiple processes may add at the same time

            var versionFolderPathContext = new VersionFolderPathContext(
                packageIdentity,
                globalPackagesFolder,
                NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            await NuGetPackageUtils.InstallFromSourceAsync(
                stream => packageStream.CopyToAsync(stream),
                versionFolderPathContext,
                token: token);

            var package = GetPackage(packageIdentity, settings);
            Debug.Assert(package.PackageStream.CanSeek);
            Debug.Assert(package.PackageReader != null);

            return package;
        }
    }
}
