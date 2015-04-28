using System;
using System.IO;

namespace NuGet.Configuration.Test
{
    public class TestDirectory : IDisposable
    {
        public TestDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }

        public static implicit operator string (TestDirectory directory)
        {
            return directory.Path;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}