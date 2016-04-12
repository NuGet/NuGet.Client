using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Protocol
{
    public interface IHttpRetryHandler
    {
        Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            Func<HttpRequestMessage> requestFactory,
            HttpCompletionOption completionOption,
            ILogger log,
            CancellationToken cancellationToken);
    }
}