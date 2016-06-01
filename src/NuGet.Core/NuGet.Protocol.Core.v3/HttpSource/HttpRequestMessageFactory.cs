// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using NuGet.Common;

namespace NuGet.Protocol
{
    /// <summary>
    /// Factory class containing methods facilitating creation of <see cref="HttpRequestMessage"/> 
    /// with additional custom parameters.
    /// </summary>
    public static class HttpRequestMessageFactory
    {
        /// <summary>
        /// Creates an instance of <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="method">Desired HTTP verb</param>
        /// <param name="requestUri">Request URI</param>
        /// <param name="log">Logger instance to be attached</param>
        /// <returns>Instance of <see cref="HttpRequestMessage"/></returns>
        public static HttpRequestMessage Create(HttpMethod method, string requestUri, ILogger log)
        {
            if (requestUri == null)
            {
                throw new ArgumentNullException(nameof(requestUri));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var request = new HttpRequestMessage(method, requestUri);
            request.SetLogger(log);
            return request;
        }

        /// <summary>
        /// Creates an instance of <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="method">Desired HTTP verb</param>
        /// <param name="requestUri">Request URI</param>
        /// <param name="log">Logger instance to be attached</param>
        /// <returns>Instance of <see cref="HttpRequestMessage"/></returns>
        public static HttpRequestMessage Create(HttpMethod method, Uri requestUri, ILogger log)
        {
            if (requestUri == null)
            {
                throw new ArgumentNullException(nameof(requestUri));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var request = new HttpRequestMessage(method, requestUri);
            request.SetLogger(log);
            return request;
        }
    }
}