using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [NuGetResourceProviderMetadata(typeof(V3ReportAbuseResource), "V3ReportAbuseResource", NuGetResourceProviderPositions.Last)]
    public class ReportAbuseResourceProvider : INuGetResourceProvider
    {
        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3ReportAbuseResource resource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);
            if (serviceIndex != null)
            {
                Uri templateUrl = serviceIndex[ServiceTypes.ReportAbuse].FirstOrDefault();
                if (templateUrl != null)
                {
                    resource = new V3ReportAbuseResource(templateUrl);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
