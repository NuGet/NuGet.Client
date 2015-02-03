using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [NuGetResourceProviderMetadata(typeof(V3ReportAbuseResouce), "V3ReportAbuseResouce", NuGetResourceProviderPositions.Last)]
    public class ReportAbuseResourceProvider : INuGetResourceProvider
    {
        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3ReportAbuseResouce resource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);
            if (serviceIndex != null)
            {
                Uri templateUrl = serviceIndex["ReportAbuse/3.0.0-rc"].FirstOrDefault();
                if (templateUrl != null)
                {
                    resource = new V3ReportAbuseResouce(templateUrl);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
