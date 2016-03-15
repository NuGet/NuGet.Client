// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class HttpHandlerResourceV3Provider : ResourceProvider
    {
        // Only one source may prompt at a time
        private readonly static SemaphoreSlim _credentialPromptLock = new SemaphoreSlim(1, 1);
        internal const int MaxAuthRetries = 3;

        public HttpHandlerResourceV3Provider()
            : base(typeof(HttpHandlerResource),
                  nameof(HttpHandlerResourceV3Provider),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            Debug.Assert(source.PackageSource.IsHttp, "HTTP handler requested for a non-http source.");

            HttpHandlerResourceV3 curResource = null;

            if (source.PackageSource.IsHttp)
            {
                var clientHandler = CreateCredentialHandler(source.PackageSource);

                // replace the handler with the proxy aware handler
                curResource = CreateHandler(source.PackageSource);
            }

            return Task.FromResult(new Tuple<bool, INuGetResource>(curResource != null, curResource));
        }

        private static HttpHandlerResourceV3 CreateHandler(PackageSource packageSource)
        {
            var clientHandler = CreateCredentialHandler(packageSource);

            // HTTP handler pipeline can be injected here, around the client handler
            var messageHandler = clientHandler;

            var resource = new HttpHandlerResourceV3(clientHandler, messageHandler);

            return resource;
        }

#if NETSTANDARD1_5
        public static HttpClientHandler CreateCredentialHandler(PackageSource packageSource)
        {
            var handler = new HttpClientHandler();
            handler.AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);
            return handler;
        }
#else

        public static HttpClientHandler CreateCredentialHandler(PackageSource packageSource)
        {
            var uri = new Uri(packageSource.Source);
            var proxy = ProxyCache.Instance.GetProxy(uri);

            if (proxy != null
                && proxy.Credentials == null)
            {
                proxy.Credentials = CredentialCache.DefaultCredentials;
            }

            if (proxy != null)
            {
                ProxyCache.Instance.Add(proxy);
            }

            return new CredentialPromptWebRequestHandler(proxy);
        }

        private class CredentialPromptWebRequestHandler : WebRequestHandler
        {
            private int _authRetries;
            private Guid _lastAuthId = Guid.NewGuid();

            public CredentialPromptWebRequestHandler(IWebProxy proxy)
            {
                Proxy = proxy;
                AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                while (true)
                {
                    // Store the auth start before sending the request
                    var beforeAuthId = _lastAuthId;

                    try
                    {
                        var response = await base.SendAsync(request, cancellationToken);
                        if (HttpHandlerResourceV3.ProxyPassed != null && Proxy != null)
                        {
                            HttpHandlerResourceV3.ProxyPassed(Proxy);
                        }

                        return response;
                    }
                    catch (HttpRequestException ex)
                    {
                        if (ProxyAuthenticationRequired(ex) &&
                            HttpHandlerResourceV3.PromptForProxyCredentials != null)
                        {
                            ICredentials currentCredentials = Proxy.Credentials;

                            try
                            {
                                await _credentialPromptLock.WaitAsync();

                                // Check if the credentials have already changed
                                if (beforeAuthId != _lastAuthId)
                                {
                                    continue;
                                }

                                // Limit the number of retries
                                _authRetries++;
                                if (_authRetries >= MaxAuthRetries)
                                {
                                    throw;
                                }

                                // prompt use for proxy credentials.
                                var credentials = await HttpHandlerResourceV3
                                    .PromptForProxyCredentials(request.RequestUri, Proxy, cancellationToken);

                                if (credentials == null)
                                {
                                    throw;
                                }

                                // use the user provided credential to send the request again.
                                Proxy.Credentials = credentials;

                                // Mark that the credentials have been updated
                                _lastAuthId = Guid.NewGuid();
                            }
                            finally
                            {
                                _credentialPromptLock.Release();
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            // Returns true if the cause of the exception is proxy authentication failure
            private bool ProxyAuthenticationRequired(Exception ex)
            {
                var webException = ex.InnerException as WebException;
                if (webException == null)
                {
                    return false;
                }

                var response = webException.Response as HttpWebResponse;
                return response?.StatusCode == HttpStatusCode.ProxyAuthenticationRequired;
            }
        }

#endif
    }
}