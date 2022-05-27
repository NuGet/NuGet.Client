// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using NuGet.Protocol.Core.Types;

namespace Test.Utility
{
    public class TestHttpHandler : HttpHandlerResource
    {
#if NETFRAMEWORK
        private WinHttpHandler _messageHandler;
        public TestHttpHandler(WinHttpHandler messageHandler)
        {
            _messageHandler = messageHandler;
        }
        public override WinHttpHandler ClientHandler
        {
            get { return _messageHandler; }
        }
#else
        private HttpClientHandler _messageHandler;

        public TestHttpHandler(HttpClientHandler messageHandler)
        {
            _messageHandler = messageHandler;
        }

        public override HttpClientHandler ClientHandler
        {
            get { return _messageHandler; }
        }
#endif

        public override HttpMessageHandler MessageHandler
        {
            get
            {
                return _messageHandler;
            }
        }
    }
}
