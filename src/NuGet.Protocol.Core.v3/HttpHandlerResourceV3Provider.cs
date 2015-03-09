using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    public class HttpHandlerResourceV3Provider : ResourceProvider
    {
        private static readonly string[] _authenticationSchemes = new[] { "Basic", "NTLM", "Negotiate" };

        public HttpHandlerResourceV3Provider()
            : base(typeof(HttpHandlerResource), "HttpHandlerResourceV3Provider", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            HttpHandlerResourceV3 curResource = null;

#if !DNXCORE50
			// Everyone gets a dataclient
			var HttpHandler = TryGetCredentialAndProxy(source.PackageSource) ?? DataClient.DefaultHandler;
            curResource = new HttpHandlerResourceV3(HttpHandler);
#endif

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }

#if !DNXCORE50
		private HttpMessageHandler TryGetCredentialAndProxy(PackageSource packageSource) 
        {
            Uri uri = new Uri(packageSource.Source);
            var proxy = ProxyCache.Instance.GetProxy(uri);
            var credential = CredentialStore.Instance.GetCredentials(uri);

            if (proxy != null && proxy.Credentials == null) {
                proxy.Credentials = CredentialCache.DefaultCredentials;
            }

            if (credential == null && !String.IsNullOrEmpty(packageSource.UserName) && !String.IsNullOrEmpty(packageSource.Password)) 
            {
                var cache = new CredentialCache();
                foreach (var scheme in _authenticationSchemes)
                {
                    cache.Add(uri, scheme, new NetworkCredential(packageSource.UserName, packageSource.Password));
                }
                credential = cache;
            }

            if (proxy == null && credential == null) return null;
            else
            {
                if(proxy != null) ProxyCache.Instance.Add(proxy);
                if (credential != null) CredentialStore.Instance.Add(uri, credential);
                return new WebRequestHandler()
                {
                    Proxy = proxy,
                    Credentials = credential
                };
            }


        }
#endif
    }
}
