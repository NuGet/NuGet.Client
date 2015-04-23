using System;
using NuGet.Packaging;

namespace NuGet.Protocol.Core.v3.LocalRepositories
{
    public class CachedPackageInfo
    {
        public string Path { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public NuspecReader Reader { get; set; }
    }
}
