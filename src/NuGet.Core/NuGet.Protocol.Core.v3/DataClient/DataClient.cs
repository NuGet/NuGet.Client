// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
#if !DNXCORE50
using System.Net.Cache;
#endif

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// A NuGet http client with support for authentication, proxies, and caching.
    /// </summary>
    public sealed class DataClient : HttpClient
    {
        private bool _disposed;

        /// <summary>
        /// Use a HttpHandlerResource containing a message handler.
        /// </summary>
        public DataClient(HttpHandlerResource handlerResource)
            : this(handlerResource.MessageHandler)
        {
        }

        /// <summary>
        /// Raw constructor that allows full overriding of all caching and default DataClient behavior.
        /// </summary>
        public DataClient(HttpMessageHandler handler)
            : base(handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // Set user agent
            var userAgent = UserAgent.UserAgentString;
            UserAgent.SetUserAgent(this, userAgent);
        }

        /// <summary>
        /// Default caching handler used by the data client
        /// </summary>
        public static HttpHandlerResourceV3 CreateHandler(HttpClientHandler clientHandler)
        {
            // Retry handler wrapping the client
            var messageHandler = new RetryHandler(clientHandler, maxTries: 3);

            return new HttpHandlerResourceV3(clientHandler, messageHandler);
        }

        /// <summary>
        /// Retrieve a json file with caching
        /// </summary>
        public async Task<JObject> GetJObjectAsync(Uri address, CancellationToken token)
        {
            var response = await GetAsync(address, token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return await Task.Run(() =>
                {
                    return JObject.Parse(json);
                });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
