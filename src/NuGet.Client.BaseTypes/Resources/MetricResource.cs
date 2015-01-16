using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public abstract class MetricResource : INuGetResource
    {
        public abstract Task RecordMetric(IDictionary<string, string> metadata, CancellationToken token);
    }
}
