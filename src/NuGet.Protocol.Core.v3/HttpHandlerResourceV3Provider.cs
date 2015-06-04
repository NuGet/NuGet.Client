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
        private static readonly string[] _authenticationSchemes = new[] { "Basic", "NTLM", "Negotiate" };

        public HttpHandlerResourceV3Provider()
            : base(typeof(HttpHandlerResource),
                  nameof(HttpHandlerResourceV3Provider),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            HttpHandlerResourceV3 curResource = null;

            // Everyone gets a dataclient
            var HttpHandler = TryGetCredentialAndProxy(source.PackageSource) ?? DataClient.DefaultHandler;
            curResource = new HttpHandlerResourceV3(HttpHandler);

            return Task.FromResult(new Tuple<bool, INuGetResource>(curResource != null, curResource));
        }

        private HttpMessageHandler TryGetCredentialAndProxy(PackageSource packageSource)
        {
#if DNXCORE50
            return new HttpClientHandler();
#else
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
                var cache = new CredentialCache();
                foreach (var scheme in _authenticationSchemes)
                {
                    cache.Add(uri, scheme, new NetworkCredential(packageSource.UserName, packageSource.Password));
                }
                credential = cache;
            }

            if (proxy == null
                && credential == null)
            {
                return null;
            }
            else
            {
                if (proxy != null)
                {
                    ProxyCache.Instance.Add(proxy);
                }
                if (credential != null)
                {
                    CredentialStore.Instance.Add(uri, credential);
                }
                return new WebRequestHandler()
                {
                    Proxy = proxy,
                    Credentials = credential
                };
            }
#endif
        }
    }
}
