using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public sealed class VisualStudioUIPackageMetadata 
    {
        public VisualStudioUIPackageMetadata(string title,IEnumerable<string> authors,IEnumerable<string> owners,Uri iconUrl, Uri licenseUrl,Uri projectUrl,bool requiresLiceneseAcceptance,string description,string summary,string releaseNotes,string language,string tags,string copyright,Version minClientVersion)
        {
            Title = title;
            Authors = authors;
            Owners = owners;
            IconUrl = iconUrl;
            ProjectUrl = projectUrl;
            RequireLicenseAcceptance = requiresLiceneseAcceptance;
            Description = description;
            Summary = summary;
            ReleaseNotes = releaseNotes;
            Language = language;
            Tags = tags;
            Copyright = copyright;
            MinClientVersion = minClientVersion;
        }
        public string Title { get; private set;}
        public IEnumerable<string> Authors { get; private set;}
        public IEnumerable<string> Owners { get; private set;}
        public Uri IconUrl { get; private set;}
        public Uri LicenseUrl { get; private set;}
        public Uri ProjectUrl { get; private set;}
        public bool RequireLicenseAcceptance { get; private set;}
        public string Description { get; private set;}
        public string Summary { get; private set;}
        public string ReleaseNotes { get; private set;}
        public string Language { get; private set;}
        public string Tags { get; private set;}
        public string Copyright { get; private set;}
        //IEnumerable<PackageDependencySet> DependencySets { get; private set;} *TODOs - copy PackageDependencySet from core to client.baprivate setypes. It has Iversionspec and a whole bunch of things need to be copied or moved.
        public Version MinClientVersion { get; private set;}
    }
}
