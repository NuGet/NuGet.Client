using NuGet.Packaging.Core;
using System;
using System.IO;

namespace NuGet.Packaging
{
    public class PackagePathResolver
    {
        private readonly bool _useSideBySidePaths;
        private readonly string _rootDirectory;
        public PackagePathResolver(string rootDirectory, bool useSideBySidePaths = true)
        {
            if(String.IsNullOrEmpty(rootDirectory))
            {
                throw new ArgumentException(String.Format(Strings.StringCannotBeNullOrEmpty, "rootDirectory"));
            }
            _rootDirectory = rootDirectory;
            _useSideBySidePaths = useSideBySidePaths;
        }

        protected internal string Root
        {
            get
            {
                return _rootDirectory;
            }
        }

        public virtual string GetPackageDirectoryName(PackageIdentity packageIdentity, bool useLegacyPackageInstallPath = false)
        {
            string directoryName = packageIdentity.Id;
            if (_useSideBySidePaths)
            {
                directoryName += ".";
                // Always use legacy package install path. Otherwise, restore may be broken for packages like 'Microsoft.Web.Infrastructure.1.0.0.0', installed using old clients
                directoryName += packageIdentity.Version.ToString(); // useLegacyPackageInstallPath ? packageIdentity.Version.ToString() : packageIdentity.Version.ToNormalizedString();
            }

            return directoryName;
        }

        public virtual string GetPackageFileName(PackageIdentity packageIdentity)
        {
            string fileNameBase = packageIdentity.Id;
            if(_useSideBySidePaths)
            {
                // TODO: Nupkgs from the server will be normalized, but others might not be.
                fileNameBase += "." + packageIdentity.Version.ToNormalizedString();
            }

            return fileNameBase + PackagingCoreConstants.NupkgExtension;
        }

        public virtual string GetInstallPath(PackageIdentity packageIdentity, bool useLegacyPackageInstallPath=false)
        {
            return Path.Combine(_rootDirectory, GetPackageDirectoryName(packageIdentity, useLegacyPackageInstallPath));
        }

        public virtual string GetInstalledPath(PackageIdentity packageIdentity)
        {
            var installedPackageFilePath = GetInstalledPackageFilePath(packageIdentity);
            return String.IsNullOrEmpty(installedPackageFilePath) ? null : Path.GetDirectoryName(installedPackageFilePath);
        }

        public virtual string GetInstalledPackageFilePath(PackageIdentity packageIdentity)
        {
            return PackagePathHelper.GetInstalledPackageFilePath(packageIdentity, this);
        }
    }
}
