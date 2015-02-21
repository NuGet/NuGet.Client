using System;
using System.IO;

namespace NuGet.Packaging.Build
{
    public class PhysicalPackageFile : IPackageFile
    {
        public PhysicalPackageFile(string sourcePath, string targetPath, int? version)
        {
            PhysicalPath = sourcePath;
            Path = targetPath;
            Version = version;
        }

        public int? Version { get; }

        public string Path { get; }

        public string PhysicalPath { get; }

        public Stream GetStream()
        {
            return File.OpenRead(PhysicalPath);
        }
    }
}