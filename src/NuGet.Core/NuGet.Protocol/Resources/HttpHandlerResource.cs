// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// An HttpClient configured for the package source
    /// </summary>
    public abstract class HttpHandlerResource : INuGetResource
    {
        /// <summary>
        /// HttpClientHandler used for credential support.
        /// </summary>
#if NETFRAMEWORK
        public abstract WinHttpHandler ClientHandler { get; }
#else
        public abstract HttpClientHandler ClientHandler { get; }
#endif

        /// <summary>
        /// Message handler containing the ClientHandler.
        /// </summary>
        public abstract HttpMessageHandler MessageHandler { get; }
    }
}
