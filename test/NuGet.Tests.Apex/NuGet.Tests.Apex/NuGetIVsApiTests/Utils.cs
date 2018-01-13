using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Tests.Apex
{
    public class Utils
    {
        public static void CreatePackageInSource(string packageSource, string packageName, string packageVersion)
        {
            var package = CreatePackage(packageName, packageVersion);
            SimpleTestPackageUtility.CreatePackages(packageSource, package);
        }

        public static void CreateSignedPackageInSource(string packageSource, string packageName, string packageVersion, X509Certificate2 testCertificate)
        {
            var package = CreateSignedPackage(packageName, packageVersion, testCertificate);
            SimpleTestPackageUtility.CreatePackages(packageSource, package);
        }

        public static SimpleTestPackageContext CreateSignedPackage(string packageName, string packageVersion, X509Certificate2 testCertificate) {
            var package = CreatePackage(packageName, packageVersion);
            package.CertificateToSign = testCertificate;

            return package;
        }

        public static SimpleTestPackageContext CreatePackage(string packageName, string packageVersion)
        {
            var package = new SimpleTestPackageContext(packageName, packageVersion);
            package.Files.Clear();
            package.AddFile("lib/net45/_._");

            return package;
        }

        public static bool IsPackageInstalled(NuGetConsoleTestExtension nuGetConsole, string projectPath, string packageName, string packageVersion)
        {
            var assetsFile = GetAssetsFilePath(projectPath);
            var packagesConfig = GetPackagesConfigPath(projectPath);
            if (File.Exists(assetsFile))
            {
                return PackageExistsInLockFile(assetsFile, packageName, packageVersion);
            }
            else if (File.Exists(packagesConfig))
            {
                return nuGetConsole.IsPackageInstalled(packageName, packageVersion);
            }
            else
            {
                return false;
            }
        }

        private static bool PackageExistsInLockFile(string pathToAssetsFile, string packageName, string packageVersion)
        {
            var lockFile = new LockFileFormat().Read(pathToAssetsFile);
            var lockFileLibrary = lockFile.Libraries.SingleOrDefault(p => (String.Compare(p.Name, packageName, StringComparison.OrdinalIgnoreCase) == 0));
            return lockFileLibrary !=null && lockFileLibrary.Version.ToNormalizedString() == packageVersion;
        }

        private static string GetAssetsFilePath(string projectPath)
        {
            if(string.IsNullOrEmpty(projectPath))
            {
                return string.Empty;
            }
            else
            {
                var projectDirectory = Path.GetDirectoryName(projectPath);
                return Path.Combine(projectDirectory, "obj", "project.assets.json");
            }
        }

        private static string GetPackagesConfigPath(string projectPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            return Path.Combine(projectDirectory, "packages.config");
        }
    }
}
