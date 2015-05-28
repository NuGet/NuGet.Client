using System;
using System.IO;

namespace NuGet.Test.Utility
{
    public class TestFileSystemUtility
    {
        private static readonly string NuGetTestFolder =
            Path.Combine(Environment.GetEnvironmentVariable("temp"), "NuGetTestFolder");

        public static string CreateRandomTestFolder()
        {
            var randomFolderName = Guid.NewGuid().ToString();
            var path = Path.Combine(NuGetTestFolder, randomFolderName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteRandomTestFolders(params string[] randomTestPaths)
        {
            foreach (var randomTestPath in randomTestPaths)
            {
                DeleteRandomTestFolder(randomTestPath);
            }
        }

        private static void DeleteRandomTestFolder(string randomTestPath)
        {
            try
            {
                if (Directory.Exists(randomTestPath))
                {
                    Directory.Delete(randomTestPath, recursive: true);
                }
            }
            catch (Exception)
            {
                // Ignore exception while deleting directories
            }
        }
    }
}
