using System;
using System.IO;
using System.Text;
using System.Linq;

namespace NuGet.Configuration.Test
{
    public static class TestFilesystemUtility
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

        public static void DeleteRandomTestFolders(params string[] randomTestPaths)
        {
            foreach (var randomTestPath in randomTestPaths)
            {
                DeleteRandomTestFolder(randomTestPath);
            }
        }

        private static void DeleteRandomTestFolder(string randomTestPath)
        {
            if (Directory.Exists(randomTestPath))
            {
                Directory.Delete(randomTestPath, recursive: true);
            }
        }

        public static void CreateConfigurationFile(string configurationPath, string mockBaseDirectory, string configurationContent)
        {
            Directory.CreateDirectory(mockBaseDirectory);
            using (FileStream file = File.Create(Path.Combine(mockBaseDirectory, configurationPath)))
            {
                Byte[] info = new UTF8Encoding(true).GetBytes(configurationContent);
                file.Write(info, 0, info.Count());
            }
        }

        public static string ReadConfigurationFile(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                using (var streamReader = new StreamReader(fs))
                {
                    return RemovedLineEndings(streamReader.ReadToEnd());
                }
            }
        }
        // this method is for removing LineEndings for CI build
        public static string RemovedLineEndings(string result)
        {
            return result.Replace("\n", "").Replace("\r", "");
        }
    }
}