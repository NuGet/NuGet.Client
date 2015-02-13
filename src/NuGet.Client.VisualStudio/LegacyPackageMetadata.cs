using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio
{
    public class LegacyPackageMetadata
    {
        public string Title { get; set; }
        public IEnumerable<string> Authors { get; set; }

        public IEnumerable<string> Owners { get; set; }

        public Uri IconUrl { get; set; }

        public Uri LicenseUrl { get; set; }

        public Uri ProjectUrl { get; set; }

        public bool RequireLicenseAcceptance { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string ReleaseNotes { get; set; }

        public string Language { get; set; }

        public string Tags { get; set; }

        public string Copyright { get; set; }

        public Uri ReportAbuseUrl { get; set; }

        public int DownloadCount { get; set; }

        public DateTimeOffset? Published { get; set; }

        public Version MinClientVersion { get; set; }
    }
}
