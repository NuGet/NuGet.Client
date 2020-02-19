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
            _dir = CreatePreGenPackageForEachPlatform();
        }
        private static string CreatePreGenPackageForEachPlatform()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = Path.Combine(root, Constants.PreGenPackagesFolder);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //Create a folder for a each platform, under PreGenPackages folder.
            //For functional test on windows, 2 folders will be created.
            var platform = "";
#if IS_DESKTOP
            platform =  Constants.Windows_NetFulFrameworkFolder;
#else
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                platform =  Constants.Windows_NetCoreFolder;
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                platform = Constants.Mac_NetCoreFolder;
            }
            else
            {
                platform = Constants.Linux_NetCoreFolder;
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
