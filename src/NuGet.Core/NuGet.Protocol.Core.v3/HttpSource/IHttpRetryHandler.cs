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
            HttpRetryHandlerRequest request,
            ILogger log,
            CancellationToken cancellationToken);
    }
}