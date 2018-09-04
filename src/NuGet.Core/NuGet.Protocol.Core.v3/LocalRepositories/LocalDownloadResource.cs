using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalDownloadResource : DownloadResource
    {
        private readonly FindLocalPackagesResource _localResource;

        public LocalDownloadResource(FindLocalPackagesResource localResource)
        {
            if (localResource == null)
            {
                throw new ArgumentNullException(nameof(localResource));
            }

            _localResource = localResource;
        }

        public override Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            PackageDownloadContext downloadContext,
            string globalPackagesFolder,
            ILogger logger,
            CancellationToken token)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            // Find the package from the local folder
            LocalPackageInfo packageInfo = null;

            var sourcePackage = identity as SourcePackageDependencyInfo;

            if (sourcePackage?.DownloadUri != null)
            {
                // Get the package directly if the full path is known
                packageInfo = _localResource.GetPackage(sourcePackage.DownloadUri, logger, token);
            }
            else
            {
                // Search for the local package
                packageInfo = _localResource.GetPackage(identity, logger, token);
            }

            if (packageInfo != null)
            {
                var stream = File.OpenRead(packageInfo.Path);
                return Task.FromResult(new DownloadResourceResult(stream, packageInfo.GetReader()));
            }
            else
            {
                return Task.FromResult(new DownloadResourceResult(DownloadResourceResultStatus.NotFound));
            }
        }
    }
}
