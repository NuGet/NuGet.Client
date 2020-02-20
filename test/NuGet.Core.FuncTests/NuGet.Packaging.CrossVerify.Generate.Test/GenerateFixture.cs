using System.IO;
using NuGet.Common;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Packaging.CrossVerify.Generate.Test
{
    public class GenerateFixture
    {
        public string _dir;

        public GenerateFixture()
        {
            _dir = GeneratePackagesForEachPlatform();
        }

        private static string GeneratePackagesForEachPlatform()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = Path.Combine(root, TestFolderNames.PreGenPackagesFolder);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //Create a folder for a each platform, under PreGenPackages folder.
            //For functional test on windows, 2 folders will be created.
            string platform = string.Empty;
#if IS_DESKTOP
            platform =  TestFolderNames.Windows_NetFullFrameworkFolder;
#else
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                platform =  TestFolderNames.Windows_NetCoreFolder;
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                platform = TestFolderNames.Mac_NetCoreFolder;
            }
            else
            {
                platform = TestFolderNames.Linux_NetCoreFolder;
            }
#endif
            var pathForEachPlatform = Path.Combine(path, platform);

            if (Directory.Exists(pathForEachPlatform))
            {
                Directory.Delete(pathForEachPlatform, recursive: true);
            }
            Directory.CreateDirectory(pathForEachPlatform);

            return pathForEachPlatform;
        }
    }
}
