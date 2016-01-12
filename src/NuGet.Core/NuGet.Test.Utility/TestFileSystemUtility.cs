using System;
using System.IO;

namespace NuGet.Test.Utility
{
    public class TestFileSystemUtility
    {
        public static readonly string NuGetTestFolder =
            Path.Combine(Path.GetTempPath(), "NuGetTestFolder");

        public static TestDirectory CreateRandomTestFolder()
        {
            var randomFolderName = Guid.NewGuid().ToString();
            var path = Path.Combine(NuGetTestFolder, randomFolderName);

            if (Directory.Exists(path))
            {
                throw new InvalidOperationException("Guid colission");
            }

            Directory.CreateDirectory(path);

            return new TestDirectory(path);
        }

        public static void DeleteRandomTestFolder(string randomTestPath)
        {
            if (Directory.Exists(randomTestPath))
            {
                AssertNotTempPath(randomTestPath);

                try
                {
                    Directory.Delete(randomTestPath, recursive: true);
                }
                catch
                {
                }

            }
        }

        public static void AssertNotTempPath(string path)
        {
            var expanded = Path.GetFullPath(path).TrimEnd(new char[] { '\\', '/' });
            var expandedTempPath = Path.GetFullPath(Path.GetTempPath()).TrimEnd(new char[] { '\\', '/' });

            if (expanded.Equals(expandedTempPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Trying to delete the temp folder in a test");
            }

            if (expanded.Equals(Path.GetFullPath(NuGetTestFolder), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Trying to delete the root test folder in a test");
            }
        }
    }
}
