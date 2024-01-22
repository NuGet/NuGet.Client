using System;
using System.Linq;
using System.Text.Json.Serialization;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class DetailedVerbosityJsonFormatSearchResultPackage : NormalVerbosityJsonFormatSearchResultPackage
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("vulnerable")]
        public bool? Vulnerable { get; set; }

        [JsonPropertyName("deprecation")]
        public string Deprecation { get; set; }

        [JsonPropertyName("projectUrl")]
        public Uri ProjectUrl { get; set; }

        public DetailedVerbosityJsonFormatSearchResultPackage() : base()
        {
        }

        public DetailedVerbosityJsonFormatSearchResultPackage(IPackageSearchMetadata packageSearchMetadata, string deprecation) : base(packageSearchMetadata)
        {
            Description = packageSearchMetadata.Description;

            if (packageSearchMetadata.Vulnerabilities != null && packageSearchMetadata.Vulnerabilities.Any())
            {
                Vulnerable = true;
            }

            Deprecation = deprecation;
            ProjectUrl = packageSearchMetadata.ProjectUrl;
        }
    }
}
