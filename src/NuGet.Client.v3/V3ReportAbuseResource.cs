using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public class V3ReportAbuseResource : INuGetResource
    {
        string _reportAbuseTemplate;

        public V3ReportAbuseResource(Uri reportAbuseTemplate)
        {
            _reportAbuseTemplate = reportAbuseTemplate.OriginalString;
        }

        public Uri GetReportAbuseUrl(string id, Versioning.NuGetVersion Version)
        {
            var reportAbuseUrl = _reportAbuseTemplate
                .Replace("{id}", id)
                .Replace("{version}", Version.ToNormalizedString());

            Uri result = null;
            if (Uri.TryCreate(reportAbuseUrl, UriKind.Absolute, out result))
            {
                return result;
            }

            return null;
        }
    }
}
