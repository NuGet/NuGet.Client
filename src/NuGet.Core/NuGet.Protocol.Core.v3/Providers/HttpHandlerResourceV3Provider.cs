// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3
{
    public class HttpHandlerResourceV3Provider : ResourceProvider
    {
        public HttpHandlerResourceV3Provider()
            : base(typeof(HttpHandlerResource),
                  nameof(HttpHandlerResourceV3Provider),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            HttpHandlerResourceV3 curResource = null;

            var clientHandler = TryGetCredentialAndProxy(source.PackageSource);

            if (clientHandler == null)
            {
                curResource = DataClient.DefaultHandler;
            }
            else
            {
                // replace the handler with the proxy aware handler
                curResource = DataClient.CreateHandler(clientHandler);
            }

            return Task.FromResult(new Tuple<bool, INuGetResource>(curResource != null, curResource));
        }

#if DNXCORE50

        private HttpClientHandler TryGetCredentialAndProxy(PackageSource packageSource)
        {
            return new HttpClientHandler();
        }
#else

        private HttpClientHandler TryGetCredentialAndProxy(PackageSource packageSource)
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

            return new CredentialPromptWebRequestHandler()
            {
                Proxy = proxy,
                Credentials = credential
            };
        }

        private class CredentialPromptWebRequestHandler : WebRequestHandler
        {
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
                            // prompt use for proxy credentials.
                            var credentials = await HttpHandlerResourceV3
                                .PromptForProxyCredentials(request.RequestUri, Proxy, cancellationToken);

                            if (credentials == null)
                            {
                                throw;
                            }

                            // use the user provider credential to send the request again.
                            Proxy.Credentials = credentials;
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