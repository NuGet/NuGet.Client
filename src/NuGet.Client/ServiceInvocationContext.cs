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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification="IDisposable is not being used to free resources here.")]
    public class ServiceInvocationContext : IDisposable
    {
        private HttpClient _client;

        /// <summary>
        /// Gets the ID of this invocation.
        /// </summary>
        public string InvocationId { get; private set; }
        
        /// <summary>
        /// Gets a <see cref="TraceContext"/> that can be used to write trace events in the context of this invocation.
        /// </summary>
        public TraceContext Trace { get; private set; }
        
        /// <summary>
        /// Gets the base URL of the <see cref="NuGetRepository"/> associated with this invocation.
        /// </summary>
        public Uri BaseUrl { get; private set; }

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that can be used to check for cancellation.
        /// </summary>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Constructs a new <see cref="ServiceInvocationContext"/> using the specified <see cref="HttpClient"/>, <see cref="ITraceSink"/>, base URL and <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> object to use to make Http Requests.</param>
        /// <param name="trace">The <see cref="ITraceSink"/> object to write trace events to.</param>
        /// <param name="baseUrl">The base URL of the <see cref="NuGetRepository"/> associated with this invocation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to check for cancellation.</param>
        public ServiceInvocationContext(HttpClient httpClient, ITraceSink trace, Uri baseUrl, CancellationToken cancellationToken)
        {
            Guard.NotNull(httpClient, "httpClient");
            Guard.NotNull(trace, "trace");
            Guard.NotNull(baseUrl, "baseUrl");

            InvocationId = Tracing.GetNextInvocationId().ToString(CultureInfo.InvariantCulture);
            _client = httpClient;
            Trace = new TraceContext(InvocationId, trace);
            BaseUrl = baseUrl;
            CancellationToken = cancellationToken;

            Trace.Start();
        }

        /// <summary>
        /// Ends the invocation represented by this <see cref="ServiceInvocationContext"/> and writes the <see cref="ITraceSink.End"/> event.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "IDisposable is not being used to free resources here.")]
        [SuppressMessage("Microsoft.Usage", "CA1816:CallGCSuppressFinalizeCorrectly", Justification = "IDisposable is not being used to free resources here.")]
        public void Dispose()
        {
            Trace.End();
        }

        /// <summary>
        /// Sends an HTTP Request in the context of this invocation.
        /// </summary>
        /// <param name="request">The <see cref="HttpRequestMessage"/> to send</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the request.</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        /// <returns>A <see cref="Task"/> that will complete with the received Http Response in the form of a <see cref="HttpResponseMessage"/> object</returns>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification="Default parameters are required by the Caller Member Info attributes")]
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

        /// <summary>
        /// Sends an HTTP GET Request in the context of this invocation.
        /// </summary>
        /// <param name="url">The URL, relative to the Repository Root, to send the request to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the request.</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        /// <returns>A <see cref="Task"/> that will complete with the received Http Response in the form of a <see cref="HttpResponseMessage"/> object</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpRequestMessage should not be disposed until after the HttpResponseMessage is disposed")]
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public virtual Task<HttpResponseMessage> GetAsync(Uri url, CancellationToken cancellationToken, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(url, "url");

            using (Trace.EnterExit())
            {
                return SendAsync(new HttpRequestMessage(HttpMethod.Get, ResolveUrl(url)), cancellationToken, methodName, filePath, line);
            }
        }

        /// <summary>
        /// Gets the fully-qualified absolute URL for a provided URL that is relative to the Repository Root.
        /// </summary>
        /// <param name="relativeUrl">The repository-relative URL</param>
        /// <returns>The fully-qualified absolute URL</returns>
        public virtual Uri ResolveUrl(Uri relativeUrl)
        {
            using (Trace.EnterExit())
            {
                return new Uri(BaseUrl, relativeUrl);
            }
        }
    }
}
