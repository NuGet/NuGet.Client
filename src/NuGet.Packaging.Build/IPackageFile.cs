using System;
using System.IO;

namespace NuGet.Packaging.Build
{
    public interface IPackageFile
    {
        int? Version { get; }
        string Path { get; }
        Stream GetStream();
    }
}