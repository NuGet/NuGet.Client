using NuGet.Client;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio
{
    /// <summary>
    /// Model for Search results displayed by Visual Studio Package Manager dialog UI.
    /// </summary>
    public sealed class UISearchMetadata
    {
        public UISearchMetadata(string id,NuGetVersion version,string summary,Uri iconUrl,IEnumerable<NuGetVersion> versions,UIPackageMetadata latestPackageMetadata)
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
        public UIPackageMetadata LatestPackageMetadata { get; private set; }

    }
}

