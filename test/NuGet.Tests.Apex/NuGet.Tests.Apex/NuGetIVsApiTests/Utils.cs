using System;
using System.IO;
using System.Linq;
using NuGet.ProjectModel;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    public class Utils
    {
        public static void CreatePackageInSource(string packageSource, string packageName, string packageVersion)
        {
            var package = new SimpleTestPackageContext(packageName, packageVersion);
            package.Files.Clear();
            package.AddFile("lib/net45/_._");
            SimpleTestPackageUtility.CreatePackages(packageSource, package);
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
