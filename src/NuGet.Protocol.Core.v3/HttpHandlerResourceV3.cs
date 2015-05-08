// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Resource wrapper for an HttpClient
    /// </summary>
    public class HttpHandlerResourceV3 : HttpHandlerResource
    {
        private readonly HttpMessageHandler _messageHandler;

        public HttpHandlerResourceV3(HttpMessageHandler messageHandler)
        {
            if (messageHandler == null)
            {
                throw new ArgumentNullException("messageHandler");
            }

            _messageHandler = messageHandler;
        }

        public override HttpMessageHandler MessageHandler
        {
            get { return _messageHandler; }
        }
    }
}
