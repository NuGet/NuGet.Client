using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Protocol
{
    public class FindLocalPackagesResourceV2 : FindLocalPackagesResource
    {
        public FindLocalPackagesResourceV2(string root)
        {
            Root = root;
        }

        public override IEnumerable<LocalPackageInfo> FindPackagesById(string id, ILogger logger, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var packages = LocalFolderUtility.GetPackagesV2(Root, id, logger);

            // Filter out any duplicates that may appear in the folder multiple times.
            return LocalFolderUtility.GetDistinctPackages(packages);
        }

        public override LocalPackageInfo GetPackage(Uri path, ILogger logger, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            return LocalFolderUtility.GetPackage(path, logger);
        }

        public override LocalPackageInfo GetPackage(PackageIdentity identity, ILogger logger, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            return LocalFolderUtility.GetPackageV2(Root, identity, logger);
        }

        public override IEnumerable<LocalPackageInfo> GetPackages(ILogger logger, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var packages = LocalFolderUtility.GetPackagesV2(Root, logger);

            // Filter out any duplicates that may appear in the folder multiple times.
            return LocalFolderUtility.GetDistinctPackages(packages);
        }
    }
}
