using System;
using System.IO;

namespace NuGet.Test.Utility
{
    public class TestFileSystemUtility
    {
        public static readonly string NuGetTestFolder =
            Path.Combine(Environment.GetEnvironmentVariable("temp"), "NuGetTestFolder");

        public static TestDirectory CreateRandomTestFolder()
        {
            var randomFolderName = Guid.NewGuid().ToString();
            var path = Path.Combine(NuGetTestFolder, randomFolderName);

            if (Directory.Exists(path))
            {
                throw new InvalidOperationException("Guid collission");
            }

            Directory.CreateDirectory(path);

            return new TestDirectory(path);
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
