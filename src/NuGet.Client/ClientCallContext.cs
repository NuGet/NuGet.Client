using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Client.Diagnostics;

namespace NuGet.Client
{
    /// <summary>
    /// Context object that wraps up all the information about a specific client call
    /// </summary>
    internal class ClientCallContext
    {
        private HttpClient _client;

        public string InvocationId { get; private set; }
        public Tracer Trace { get; private set; }
        public Uri BaseUrl { get; private set; }

        public ClientCallContext(HttpClient httpClient, ITraceSink trace, Uri baseUrl)
        {
            Guard.NotNull(httpClient, "httpClient");
            Guard.NotNull(trace, "trace");
            Guard.NotNull(baseUrl, "baseUrl");

            InvocationId = Tracing.GetNextInvocationId().ToString(CultureInfo.InvariantCulture);
            _client = httpClient;
            Trace = new Tracer(InvocationId, trace);
            BaseUrl = baseUrl;
        }

        public virtual async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(request, "request");
            using (Trace.EnterExit())
            {

                Trace.SendRequest(request, methodName, filePath, line);
                HttpResponseMessage response;
                try
                {
                    response = await _client.SendAsync(request, cancellationToken);
                }
                catch (Exception ex)
                {
                    Trace.Error(ex, methodName, filePath, line);
                    throw;
                }
                Trace.ReceiveResponse(response, methodName, filePath, line);
                return response;
            }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification="HttpRequestMessage should not be disposed until after the HttpResponseMessage is disposed")]
        public virtual Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNullOrEmpty(url, "url");

            using (Trace.EnterExit())
            {
                return SendAsync(new HttpRequestMessage(HttpMethod.Get, ResolveUrl(url)), cancellationToken, methodName, filePath, line);
            }
        }

        public virtual Uri ResolveUrl(string relativeUrl)
        {
            using (Trace.EnterExit())
            {
                return new Uri(BaseUrl, relativeUrl ?? "/");
            }
        }
    }
}
