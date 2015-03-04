using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Collections.Generic;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// Model for search results shown by PowerShell console search.
    /// *TODOS: Should we extract out ID,version and summary to a base search model ? 
    /// </summary>
    public sealed class PSSearchMetadata
    {
        public PSSearchMetadata(PackageIdentity identity, IEnumerable<NuGetVersion> versions, string summary)
        {
            Identity = identity;
            Versions = versions;
            Summary = summary;
        }
        public PackageIdentity Identity { get; private set; }
        public NuGetVersion Version { get; private set; }
        public IEnumerable<NuGetVersion> Versions { get; private set; }
        public string Summary { get; private set; }
       
    }
}
