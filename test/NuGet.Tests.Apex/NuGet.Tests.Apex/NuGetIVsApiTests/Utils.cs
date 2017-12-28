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

        public static bool PackageExistsInLockFile(string projectPath, string packageName, string packageVersion)
        {
            var assetsFilePath = GetAssetsFilePath(projectPath);
            if(File.Exists(assetsFilePath))
            {
                var lockFile = new LockFileFormat().Read(assetsFilePath);
                var lockFileLibrary = lockFile.Libraries.SingleOrDefault(p => (String.Compare(p.Name, packageName, StringComparison.OrdinalIgnoreCase) == 0));
                return lockFileLibrary !=null && lockFileLibrary.Version.ToNormalizedString() == packageVersion;
            }

            return false;
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
    }
}
