using NuGet.Versioning;
using System;

namespace NuGet.CommandLine
{
    [Serializable]
    public class AssemblyMetadata
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Company { get; set; }
        public string Copyright { get; set; }
    }
}
