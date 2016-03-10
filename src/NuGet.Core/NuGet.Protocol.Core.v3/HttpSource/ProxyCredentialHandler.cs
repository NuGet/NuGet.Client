// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// A message handler responsible for retrying request for authenticated proxies
    /// with missing credentials.
    /// </summary>
    internal class ProxyCredentialHandler : DelegatingHandler
    {
        private readonly IProxyCredentialDriver _credentialsDriver;
        private readonly HttpClientHandler _clientHandler;

        public ProxyCredentialHandler(
            HttpClientHandler clientHandler,
            IProxyCredentialDriver credentialsDrvier)
            : base(clientHandler)
        {
            if (credentialsDrvier == null)
            {
                throw new ArgumentNullException(nameof(credentialsDrvier));
            }

            _credentialsDriver = credentialsDrvier;

            if (clientHandler == null)
            {
                throw new ArgumentNullException(nameof(clientHandler));
            }

            _clientHandler = clientHandler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    var response = await base.SendAsync(request, cancellationToken);

                    if (response.StatusCode != HttpStatusCode.ProxyAuthenticationRequired)
                    {
                        return response;
                    }

                    var proxyAdress = _clientHandler.Proxy.GetProxy(request.RequestUri);

                    var proxyCredentials = await _credentialsDriver.AcquireCredentialsAsync(
                        proxyAdress, _clientHandler.Proxy, cancellationToken);

                    if (proxyCredentials == null)
                    {
                        return response;
                    }
                }
                catch (HttpRequestException ex) when (ProxyAuthenticationRequired(ex))
                {
                    var proxyAdress = _clientHandler.Proxy.GetProxy(request.RequestUri);

                    var proxyCredentials = await _credentialsDriver.AcquireCredentialsAsync(
                        proxyAdress, _clientHandler.Proxy, cancellationToken);

                    if (proxyCredentials == null)
                    {
                        throw;
                    }
                }
            }
        }

#if !NETSTANDARD1_5 && !DNXCORE50
        // Returns true if the cause of the exception is proxy authentication failure
        private static bool ProxyAuthenticationRequired(Exception ex)
        {
            var response = ExtractResponse(ex);
            return response?.StatusCode == HttpStatusCode.ProxyAuthenticationRequired;
        }

        private static HttpWebResponse ExtractResponse(Exception ex)
        {
            var webException = ex.InnerException as WebException;
            var response = webException?.Response as HttpWebResponse;
            return response;
        }
#else
        private static bool ProxyAuthenticationRequired(Exception ex)
        {
            return false;
        }
#endif
    }
}