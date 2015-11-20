using System;
using System.IO;

namespace NuGet.Test.Utility
{
    public class TestPackageInfo : IDisposable
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public FileInfo File { get; set; }

        public void Dispose()
        {
            File.Delete();
        }
    }
}
