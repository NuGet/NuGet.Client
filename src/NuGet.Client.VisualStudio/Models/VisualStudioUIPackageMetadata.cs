using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public sealed class VisualStudioUIPackageMetadata 
    {
        public VisualStudioUIPackageMetadata(NuGetVersion version,string summary,string description,string authors,string owners,Uri iconUrl, Uri licenseUrl,Uri projectUrl,string tags,int downloadCount,DateTimeOffset? published,IEnumerable<VisualStudioUIPackageDependencySet> dependencySet, bool hasDependencies)
        {
            Version = version;
            Summary = summary;
            Description = description;
            Authors = authors;
            Owners = owners;
            IconUrl = iconUrl;
            LicenseUrl = licenseUrl;
            ProjectUrl = projectUrl;           
            Description = description;
            Summary = summary;          
            Tags = tags;
            DependencySets = dependencySet;
            HasDependencies = hasDependencies;
        }
        public NuGetVersion Version { get; private set; }
        public string Summary { get; private set; }

        public string Description { get; private set; }

        public string Authors { get; private set; }

        public string Owners { get; private set; }

        public Uri IconUrl { get; private set; }

        public Uri LicenseUrl { get; private set; }

        public Uri ProjectUrl { get; private set; }

        public string Tags { get; private set; }

        public int DownloadCount { get; private set; }

        public DateTimeOffset? Published { get; private set; }

        public IEnumerable<VisualStudioUIPackageDependencySet> DependencySets { get; private set; }

        // This property is used by data binding to display text "No dependencies"
        public bool HasDependencies { get; private set; }
    }
}
