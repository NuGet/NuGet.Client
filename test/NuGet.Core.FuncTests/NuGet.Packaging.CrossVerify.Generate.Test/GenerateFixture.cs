using System.IO;
using NuGet.Common;
using NuGet.Test.Utility;

namespace NuGet.Packaging.CrossVerify.Generate.Test
{
    public class GenerateFixture
    {
        private const string PreGeneratePackageFolderName = "PreGenPackages";

        public string _dir;

        public GenerateFixture()
        {
            _dir = CreatePreGenPackageForEachPlatform();
        }
        private static string CreatePreGenPackageForEachPlatform()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = Path.Combine(root, PreGeneratePackageFolderName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //Create a folder for a each platform, under PreGenPackages folder.
            //For functional test on windows, 2 folders will be created.
            var platform = "";
#if IS_DESKTOP
            platform = "Windows_NetFulFramework";
#else
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                platform =  "Windows_NetCore";
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                platform = "Mac_NetCore";
            }
            else
            {
                platform = "Linux_NetCore";
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
