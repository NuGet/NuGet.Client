using NuGet.Client;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    /// <summary>
    /// Model for Search results displayed by Visual Studio Package Manager dialog UI.
    /// </summary>
    public sealed class VisualStudioUISearchMetadata
    {
        public VisualStudioUISearchMetadata(string id,NuGetVersion version,string summary,Uri iconUrl,IEnumerable<NuGetVersion> versions,VisualStudioUIPackageMetadata latestPackageMetadata)
        {
            Id = id;
            Version = version;
            Summary = summary;
            IconUrl = iconUrl;
            Versions = versions;
            LatestPackageMetadata = latestPackageMetadata;
        }
        public string Id { get; private set; }
        public NuGetVersion Version { get; private set; }
        public string Summary { get; private set; }
        public Uri IconUrl { get; private set; }
        public IEnumerable<NuGetVersion> Versions { get; private set; }
        public VisualStudioUIPackageMetadata LatestPackageMetadata { get; private set; }

    }
}

