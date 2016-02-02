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
        internal const int MaxAuthRetries = 10;

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
            var handler = CreateCredentialHandler(packageSource);

            var retryHandler = new RetryHandler(handler, maxTries: 3);

            var resource = new HttpHandlerResourceV3(handler, retryHandler);

            return resource;
        }

#if DNXCORE50
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
            var credential = CredentialStore.Instance.GetCredentials(uri);

            if (proxy != null
                && proxy.Credentials == null)
            {
                proxy.Credentials = CredentialCache.DefaultCredentials;
            }

            if (credential == null
                && !String.IsNullOrEmpty(packageSource.UserName)
                && !String.IsNullOrEmpty(packageSource.Password))
            {
                credential = new NetworkCredential(packageSource.UserName, packageSource.Password);
            }

            if (proxy != null)
            {
                ProxyCache.Instance.Add(proxy);
            }
            if (credential != null)
            {
                CredentialStore.Instance.Add(uri, credential);
            }

            return new CredentialPromptWebRequestHandler(proxy, credential);
        }

        private class CredentialPromptWebRequestHandler : WebRequestHandler
        {
            private int _authRetries;

            public CredentialPromptWebRequestHandler(IWebProxy proxy, ICredentials credentials)
            {
                Proxy = proxy;
                Credentials = credentials;
                AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                while (true)
                {
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
                                if (!object.ReferenceEquals(currentCredentials, Proxy.Credentials))
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