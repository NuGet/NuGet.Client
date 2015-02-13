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
                throw new ArgumentException(String.Format(Strings.StringCannotBeNullOrEmpty, "rootDirectory"));
            }
            _rootDirectory = rootDirectory;
            _useSideBySidePaths = useSideBySidePaths;
        }

        public virtual string GetPackageDirectoryName(PackageIdentity packageIdentity)
        {
            string directoryName = packageIdentity.Id;
            if (_useSideBySidePaths)
            {
                // For legacy support do not normalize the version string
                directoryName += "." + packageIdentity.Version.ToString();
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

        public virtual string GetInstallPath(PackageIdentity packageIdentity)
        {
            return Path.Combine(_rootDirectory, GetPackageDirectoryName(packageIdentity));
        }
    }
}
