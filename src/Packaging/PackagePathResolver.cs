using NuGet.PackagingCore;
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
                throw new ArgumentException("rootDirectory cannot be null or empty");
            }
            _rootDirectory = rootDirectory;
            _useSideBySidePaths = useSideBySidePaths;
        }

        public virtual string GetPackageDirectoryName(PackageIdentity packageIdentity)
        {
            string directoryName = packageIdentity.Id;
            if (_useSideBySidePaths)
            {
                directoryName += "." + packageIdentity.Version;
            }

            return directoryName;
        }

        public virtual string GetPackageFileName(PackageIdentity packageIdentity)
        {
            string fileNameBase = packageIdentity.Id;
            if(_useSideBySidePaths)
            {
                fileNameBase += "." + packageIdentity.Version;
            }

            return fileNameBase + PackagingConstants.NupkgExtension;
        }

        public virtual string GetInstallPath(PackageIdentity packageIdentity)
        {
            return Path.Combine(_rootDirectory, GetPackageDirectoryName(packageIdentity));
        }
    }
}
