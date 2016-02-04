// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Net.Http;

namespace NuGet.Protocol
{
    internal static class HttpRequestMessageExtensions
    {
        /// <summary>
        /// Clones an <see cref="HttpRequestMessage" /> request.
        /// </summary>
        public static HttpRequestMessage Clone(this HttpRequestMessage request)
        {
            Debug.Assert(request.Content == null, "Cloning the request content is not yet implemented.");

            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content,
                Version = request.Version
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var property in request.Properties)
            {
                clone.Properties.Add(property);
            }

            return clone;
        }
    }
}
