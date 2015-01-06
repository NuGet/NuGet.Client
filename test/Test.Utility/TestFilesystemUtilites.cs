using System;
using System.IO;

namespace Test.Utility
{
    public static class TestFilesystemUtilites
    {
        private static readonly string NuGetTestFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NuGetTestFolder");

        public static string CreateRandomTestFolder()
        {
            var randomFolderName = Guid.NewGuid().ToString();
            var path = Path.Combine(NuGetTestFolder, randomFolderName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteRandomTestPath(string randomTestPath)
        {
            if (Directory.Exists(randomTestPath))
            {
                Directory.Delete(randomTestPath, recursive: true);
            }
        }
    }
}
