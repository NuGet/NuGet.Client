using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public static class Types
    {
        public static readonly Uri PackageSearchResult = new Uri("http://schema.nuget.org/schema#PackageSearchResult");
        public static readonly Uri PackageIdentity = new Uri("http://schema.nuget.org/schema#PackageIdentity");
        public static readonly Uri PackageDescription = new Uri("http://schema.nuget.org/schema#PackageDescription");
        public static readonly Uri PackageLicensing = new Uri("http://schema.nuget.org/schema#PackageLicensing");
        public static readonly Uri PackageDependencies = new Uri("http://schema.nuget.org/schema#PackageDependencies");
        public static readonly Uri DependencyGroup = new Uri("http://schema.nuget.org/schema#DependencyGroup");
        public static readonly Uri Dependency = new Uri("http://schema.nuget.org/schema#Dependency");
    }

    public static class ServiceUris
    {
        private const string Root = "http://schema.nuget.org/services#";
        public static readonly Uri SearchQueryService = new Uri(Root + "SearchQueryService");
        public static readonly Uri RegistrationsBaseUrl = new Uri(Root + "RegistrationsBaseUrl");
        public static readonly Uri MetricsService = new Uri(Root + "MetricsService");

        public static readonly Uri Resources = new Uri(Root + "resources");
        public static readonly Uri Version = new Uri(Root + "version");
    }

    public static class Properties
    {
        public static readonly string SubjectId = "@id";
        public static readonly string Type = "@type";

        public static readonly string PackageId = "id";
        public static readonly string Version = "version";
        public static readonly string Summary = "summary";
        public static readonly string Description = "description";
        public static readonly string Authors = "authors";
        public static readonly string Owners = "owners";
        public static readonly string IconUrl = "iconUrl";
        public static readonly string LicenseUrl = "licenseUrl";
        public static readonly string ProjectUrl = "projectUrl";
        public static readonly string Tags = "tags";
        public static readonly string DownloadCount = "downloadCount";
        public static readonly string Published = "published";
        public static readonly string RequireLicenseAcceptance = "requireLicenseAcceptance";
        public static readonly string DependencyGroups = "dependencyGroups";
        public static readonly string LatestVersion = "latestVersion";
        public static readonly string TargetFramework = "targetFramework";
        public static readonly string Dependencies = "dependencies";
        public static readonly string Range = "range";
        public static readonly string MinimumClientVersion = "minClientVersion";
        public static readonly string Language = "language";
        public static readonly string PackageContent = "packageContent";
        public static readonly string Versions = "versions";
    }
}
