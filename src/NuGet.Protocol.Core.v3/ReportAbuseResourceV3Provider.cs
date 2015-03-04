using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    public class ReportAbuseResourceV3Provider : ResourceProvider
    {
        public ReportAbuseResourceV3Provider()
            : base(typeof(ReportAbuseResourceV3), "ReportAbuseResourceV3", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ReportAbuseResourceV3 resource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);
            if (serviceIndex != null)
            {
                resource = new ReportAbuseResourceV3();

                //IEnumerable<Uri> templateUrls = serviceIndex[ServiceTypes.ReportAbuse];
                //if (templateUrls != null && templateUrls.Any())
                //{
                //    resource = new ReportAbuseResourceV3(templateUrls);
                //}
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
