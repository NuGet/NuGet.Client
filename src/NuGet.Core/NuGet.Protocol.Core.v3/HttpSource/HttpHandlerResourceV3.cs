// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using System.Threading;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Resource wrapper for an HttpClient
    /// </summary>
    public class HttpHandlerResourceV3 : HttpHandlerResource
    {
        private readonly HttpClientHandler _clientHandler;
        private readonly HttpMessageHandler _messageHandler;

        public HttpHandlerResourceV3(HttpClientHandler clientHandler, HttpMessageHandler messageHandler)
        {
            if (clientHandler == null)
            {
                throw new ArgumentNullException(nameof(clientHandler));
            }

            if (messageHandler == null)
            {
                throw new ArgumentNullException(nameof(messageHandler));
            }

            _clientHandler = clientHandler;
            _messageHandler = messageHandler;
        }

        public override HttpClientHandler ClientHandler
        {
            get { return _clientHandler; }
        }

        public override HttpMessageHandler MessageHandler
        {
            get { return _messageHandler; }
        }

        /// <summary>
        /// Function to be called to prompt user for proxy credentials.
        /// </summary> 
        public static Func<Uri, IWebProxy, CancellationToken, Task<ICredentials>> PromptForProxyCredentials
            { get; set; }

        /// <summary>
        /// Action to be called when the proxy is successfully used to make a request.
        /// </summary> 
        public static Action<IWebProxy> ProxyPassed { get; set; }

        /// <summary>
        /// Gets or sets a delegate to be invoked to prompt user for authenticated feed credentials.
        /// </summary>
        public static Func<Uri, CancellationToken, Task<ICredentials>> PromptForCredentials { get; set; }

        /// <summary>
        /// Gets or sets a delegate that is to be invoked when authenticated feed credentials are successfully
        /// used.
        /// </summary>
        public static Action<Uri, ICredentials> CredentialsSuccessfullyUsed { get; set; }
    }
}