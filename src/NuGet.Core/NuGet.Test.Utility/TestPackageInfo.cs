using System;
using System.IO;

namespace NuGet.Test.Utility
{
    public class TestPackageInfo : IDisposable
    {
        public TestPackageInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException($"{nameof(filePath)} cannot be null or empty");
            }

            File = new FileInfo(filePath);
        }

        public string Id { get; set; }
        public string Version { get; set; }
        public FileInfo File { get; }

        public void Dispose()
        {
            try
            {
                if (File.Exists)
                {
                    File.Delete();
                }
            }
            catch
            {
            }
        }
    }
}
